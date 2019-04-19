namespace RefDataCrawler

open System
open System.Net.Http

type CrawlerActor(log: PostMessage, crawlStatus: PostMessage, writeEntity: PostMessage, config: CrawlerConfig)=
    
    let client = HttpRequests.httpClient()
    
    
    let postDiscovered entityType ids =
        ids |> Seq.map (fun s -> (entityType, s)) 
            |> Seq.map ActorMessage.DiscoveredEntity 
            |> Seq.iter crawlStatus

    let entityTypeName msg =
        match msg with
        | Regions 
        | RegionIds _ ->        "region"
        | Constellations
        | ConstellationIds _ -> "constellation"
        | SolarSystems
        | SolarSystemId _ ->    "system"
        | PlanetIds _ ->        "planet"
        | AsteroidBeltIds _ ->  "belt"
        | MoonIds _ ->          "moon"
        | StarIds _ ->          "star"
        | StationIds _ ->       "station"
        | StargateIds _ ->      "stargate"
        | Groups 
        | GroupIds _ ->         "group"
        | Types
        | TypeIds _ ->          "type"
        | Categories
        | CategoryIds _ ->      "category"
        | DogmaAttributes
        | DogmaAttributeIds _ -> "dogma_attribute"
        | DogmaEffects
        | DogmaEffectIds _ ->   "dogma_effect"
        | _ -> invalidOp "Unknown type"
        
    let entityTypeIds msg =
        match msg with
        | SolarSystemId id  ->  [ id ]
        | RegionIds ids
        | ConstellationIds ids 
        | PlanetIds ids 
        | AsteroidBeltIds ids 
        | MoonIds ids 
        | StarIds ids 
        | StationIds ids 
        | GroupIds ids
        | CategoryIds ids
        | TypeIds ids
        | DogmaAttributeIds ids
        | DogmaEffectIds ids
        | StargateIds ids ->    ids |> List.ofSeq
        | _ ->                  []

    let entityTypeMsg msg =
        match msg with
        | "planet" ->   PlanetIds
        | "belt" ->     AsteroidBeltIds
        | "moon" ->     MoonIds 
        | "star" ->     StarIds
        | "station" ->  StationIds
        | "stargate" -> StargateIds
        
        | "region" ->   RegionIds
        | "constellation" -> ConstellationIds
        
        | "groups" ->   GroupIds
        | "category" -> CategoryIds
        | "type" ->     TypeIds
        | "dogma_attribute" -> DogmaAttributeIds
        | "dogma_effect" -> DogmaEffectIds
        | _ -> invalidOp "unknown type" 

    
    
    let postbackEntityTypeError post (msg: ActorMessage) resp =
        let entityType = entityTypeName msg
        sprintf "Error [%s] for %s" (string resp.Status) entityType
            |> ActorMessage.Error
            |> (log <--> crawlStatus)   
        msg |> post |> ignore

        
    let onGetServerStatus () =
        async {
            let entityType = "server_status"
            let entityId = ""
            
            postDiscovered entityType [ entityId ]

            let! serverStatus = Esi.serverStatus client
            // get the ESI server status
            // if different -> continue

            // TODO: when down:{"error":"The datasource tranquility is temporarily unavailable"}
            let msg = serverStatus |> Option.map (fun ss -> { ServerVersion.version = string ss.ServerVersion }
                                                                |> Newtonsoft.Json.JsonConvert.SerializeObject
                                                                |> (fun j -> ActorMessage.Entity (entityType, entityId, "", j)) )
            match msg with
            | Some m -> writeEntity m
            | _ -> ignore 0

            return TimeSpan.Zero
        }

    
    let onGetEntityIds (post: PostMessage) (actorMsg: ActorMessage) (getIds) (msgIds: string[] -> ActorMessage)=
        async { 
            let entityType = entityTypeName actorMsg
            let! resp = getIds client
            return match resp with
                    | Choice1Of2 entityIds ->   let entityIds = entityIds |> Seq.map string |> Array.ofSeq
                                                
                                                sprintf "Found %i %s(s)" entityIds.Length entityType |> ActorMessage.Info |> log
                        
                                                entityIds   |> Seq.chunkBySize 10
                                                            |> Seq.map msgIds 
                                                            |> Seq.iter post
                                                entityIds   |> postDiscovered entityType

                                                TimeSpan.Zero
                    | Choice2Of2 errorResp ->   postbackEntityTypeError post actorMsg errorResp
                                                // TODO: find wait
                                                TimeSpan.Zero
        }
        
        
    let onGetSystemIds (post: PostMessage) entityType =
        async {
            let! resp = Esi.systemIds client 

            return match resp with
                    | Choice1Of2 systemIds ->   let systemIds = systemIds 
                                                                    //|> Seq.filter (fun s ->  [ 30005003; 30000142; 30003072] |> Seq.contains s ) // TODO: temporary
                                                                    |> Seq.map string
                                                                    |> Array.ofSeq
                                                sprintf "Found %i %s(s)" systemIds.Length entityType |> ActorMessage.Info |> log
                                                systemIds |> Seq.map (string >> ActorMessage.SolarSystemId) |> Seq.iter post
                                                systemIds |> postDiscovered entityType
                                                TimeSpan.Zero
                    | Choice2Of2 errorResp ->   postbackEntityTypeError post ActorMessage.SolarSystems errorResp
                                                // TODO: find wait
                                                TimeSpan.Zero
        }
        

    let onEntities (postBack) entityType (req: string -> HttpRequestMessage) (ids: string[]) =
        async {
            
            ids |> String.concatenate ", " |>  sprintf "Found %s %s" entityType |> ActorMessage.Info |> log

            let reqs = ids |> Array.map (fun id -> req id |> HttpResponses.response client |> Async.map (fun r -> (id,r) ) )

            let! resps = reqs |> Async.Parallel
            
            let okResps, failResps = resps |> Array.partition (fun (id,r) -> r.Status = HttpStatus.OK)
            
            okResps 
                |> Seq.iter (fun (id,resp) -> 
                                        let etag = resp.ETag |> Option.map (fun e -> e.tag) |> Option.defaultValue ""
                                        (entityType, id, etag, resp.Message) |> ActorMessage.Entity |> writeEntity       
                                )

            // repost errors...
            if failResps.Length > 0 then
                failResps  |> Seq.map (fun (id,r) -> sprintf "Error [%s] for %s %s" (string r.Status) entityType id )
                           |> Seq.map ActorMessage.Error
                           |> Seq.iter (log <--> crawlStatus)

                let newReqs = failResps   |> Seq.map fst
                                          |> Array.ofSeq
                                          |> entityTypeMsg entityType
                newReqs |> postBack

            return resps |> Seq.map snd |> HttpResponses.maxWaitTime
            
        }
        
        
    let onSystemId (postBack: PostMessage) entityType id = 
        async {
            id |> sprintf "Found %s %s" entityType |> ActorMessage.Info |> log

            let! resp = id  |> Esi.systemRequest
                            |> HttpResponses.response client
                        
            return 
                if resp.Status = HttpStatus.OK then
                    
                    let system = Esi.toSolarSystem resp
                    
                    let etag = resp.ETag |> Option.map (fun e -> e.tag) |> Option.defaultValue ""
                    (entityType, id, etag, resp.Message) |> ActorMessage.Entity |> writeEntity
                    
                    
                    // get the stations, planets, moons, belts, etc...
                    let planetIds = (fun () -> system.Planets) |> (?>) [||]
                                        |> Array.map (fun x -> string x.PlanetId) 
                                        |> ActorMessage.PlanetIds
                    
                    let starIds = (fun () -> [| string system.StarId |]) |> (?>) [||]
                                        |> ActorMessage.StarIds

                    let moonIds = (fun () -> system.Planets) |> (?>) [||]
                                        |> Seq.collect (fun p -> p.Moons) 
                                        |> Seq.map string 
                                        |> Array.ofSeq |> ActorMessage.MoonIds

                    let beltIds = (fun () -> system.Planets) |> (?>) [||]
                                    |> Seq.collect (fun p -> p.AsteroidBelts) 
                                    |> Seq.map string
                                    |> Array.ofSeq |> ActorMessage.AsteroidBeltIds

                    let stationIds = (fun () -> system.Stations) |> (?>) [||]
                                        |> Array.map string
                                        |> ActorMessage.StationIds

                    let stargateIds = (fun () -> system.Stargates) |> (?>) [||]
                                        |> Array.map string
                                        |> ActorMessage.StargateIds
                
                

                    let entities = [ planetIds; starIds; moonIds; beltIds; stationIds; stargateIds ]
                                        |> List.filter (entityTypeIds >> List.isEmpty >> not)
                        
                    entities |> Seq.iter postBack
                    entities |> Seq.iter (fun e -> postDiscovered (entityTypeName e) (entityTypeIds e))
                                
                    TimeSpan.Zero
                else
                    sprintf "Error [%s] for %s %s" (string resp.Status) entityType id
                           |> ActorMessage.Error
                           |> (log <--> crawlStatus)

                    id |> ActorMessage.SolarSystemId |> postBack

                    [resp] |> HttpResponses.maxWaitTime 
                    
                
        }


    let pipe = MessageInbox.Start(fun inbox -> 
        
        let post = inbox.Post
        let rec getNext(wait: TimeSpan) = async {
               
            if(wait.TotalMilliseconds > 0.) then
                wait.ToString() |> sprintf "Throttling back for %s..." |> ActorMessage.Info |> log
                do! Async.Sleep(int wait.TotalMilliseconds)
            
            let! inMsg = inbox.Receive()

            let! nextWait = async {
                                    match inMsg with
                                    | ServerStatus ->           return! onGetServerStatus()

                                    | Regions ->                return! (onGetEntityIds post (inMsg) Esi.regionIds ActorMessage.RegionIds)
                                    | RegionIds ids ->          return! (onEntities post (entityTypeName inMsg) Esi.regionRequest ids )

                                    | Constellations ->         return! (onGetEntityIds post (inMsg) Esi.constellationIds ActorMessage.ConstellationIds)
                                    | ConstellationIds ids ->   return! (onEntities post (entityTypeName inMsg) Esi.constellationRequest ids )

                                    | SolarSystems ->           return! (onGetSystemIds post (entityTypeName inMsg))
                                    | SolarSystemId id ->       return! (onSystemId post (entityTypeName inMsg) id)
                                            
                                    | PlanetIds ids ->          return! (onEntities post (entityTypeName inMsg) Esi.planetRequest ids)
                                    | AsteroidBeltIds ids ->    return! (onEntities post (entityTypeName inMsg) Esi.asteroidBeltRequest ids)
                                    | MoonIds ids ->            return! (onEntities post (entityTypeName inMsg) Esi.moonRequest ids)
                                    | StarIds ids ->            return! (onEntities post (entityTypeName inMsg) Esi.starRequest ids)
                                    | StationIds ids ->         return! (onEntities post (entityTypeName inMsg) Esi.stationRequest ids)
                                    | StargateIds ids ->        return! (onEntities post (entityTypeName inMsg) Esi.stargateRequest ids)

                                    | Groups ->                 return! (onGetEntityIds post (inMsg) Esi.groupIds ActorMessage.GroupIds)
                                    | GroupIds ids ->           return! (onEntities post (entityTypeName inMsg) Esi.groupRequest ids)
                                    | Categories ->             return! (onGetEntityIds post (inMsg) Esi.categoryIds ActorMessage.CategoryIds)
                                    | CategoryIds ids ->        return! (onEntities post (entityTypeName inMsg) Esi.categoryRequest ids)
                                    | Types ->                  return! (onGetEntityIds post (inMsg) Esi.typeIds ActorMessage.TypeIds)
                                    | TypeIds ids ->            return! (onEntities post (entityTypeName inMsg) Esi.typeRequest ids)
                                    | DogmaAttributes ->        return! (onGetEntityIds post (inMsg) Esi.dogmaAttributeIds ActorMessage.DogmaAttributeIds)
                                    | DogmaAttributeIds ids ->  return! (onEntities post (entityTypeName inMsg) Esi.dogmaAttributeRequest ids)
                                    | DogmaEffects ->           return! (onGetEntityIds post (inMsg) Esi.dogmaEffectIds ActorMessage.DogmaEffectIds)
                                    | DogmaEffectIds ids ->     return! (onEntities post (entityTypeName inMsg) Esi.dogmaEffectRequest ids)
                                    | _ -> return TimeSpan.Zero
                                  }
            return! getNext(nextWait)
        }
        
        getNext(TimeSpan.Zero)
    )

    do pipe.Error.Add(Actors.postException typeof<CrawlerActor>.Name log)

    member __.Post(msg: ActorMessage) = pipe.Post msg