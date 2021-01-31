namespace RefDataCrawler

open System
open System.Net.Http

type EsiCrawlerActor(log: PostMessage, crawlStatus: PostMessage, writeEntity: PostMessage, 
                     getMetadata: (string * string[] -> Async<EntityMetadata option[]>), 
                     config: CrawlerConfig)=
    
    let client = HttpRequests.httpClient()
    
    let postDiscovered entityType ids =
        ActorMessage.DiscoveredEntities (entityType, (ids |> Array.ofSeq) ) 
            |> crawlStatus
    
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
        | MarketGroups
        | MarketGroupIds _ ->   "market_group"
        | NpcCorps 
        | NpcCorpsIds _ ->      "npccorps"
        | ServerStatus ->       "server_status"
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
        | MarketGroupIds ids
        | NpcCorpsIds ids
        | StargateIds ids ->    ids |> List.ofSeq
        | _ ->                  []

    let entityTypeMsg msg =
        match msg with
        | "planet" ->           PlanetIds
        | "belt" ->             AsteroidBeltIds
        | "moon" ->             MoonIds 
        | "star" ->             StarIds
        | "station" ->          StationIds
        | "stargate" ->         StargateIds
        
        | "region" ->           RegionIds
        | "constellation" ->    ConstellationIds
        
        | "groups" ->           GroupIds
        | "category" ->         CategoryIds
        | "type" ->             TypeIds
        | "dogma_attribute" ->  DogmaAttributeIds
        | "dogma_effect" ->     DogmaEffectIds
        | "market_group" ->     MarketGroupIds
        | "npccorps" ->         NpcCorpsIds
        | _ -> invalidOp "unknown type" 

    
    
    let postbackEntityTypeError (post: PostMessage) (msg: ActorMessage) resp =
        let entityType = entityTypeName msg
        
        let errMsg =  sprintf "Error [%s] " (string resp.Status)
                    + (match resp.Status with
                            | HttpRequestError -> resp.Message
                            | _ -> sprintf " for %s" entityType ) 
                   |> ActorMessage.Error
        errMsg |> (log <--> crawlStatus)   
        msg |> post |> ignore

        
    let onGetServerStatus post actorMsg =
        async {
            let entityType = entityTypeName actorMsg
            let entityId = "0"
            
            postDiscovered entityType [ entityId ]

            let! serverStatus = Esi.serverStatus client
            return match serverStatus with
                    | Choice1Of2 ss -> 
                                        let msg = { ServerVersion.version = string ss.ServerVersion; }
                                                        |> Newtonsoft.Json.JsonConvert.SerializeObject
                                                        |> (fun j -> ActorMessage.Entity (entityType, entityId, "", j))
                                        
                                        writeEntity msg
                                        
                                        TimeSpan.Zero
                    | Choice2Of2 errResp ->   
                                        postbackEntityTypeError post actorMsg errResp
                                        [errResp] |> HttpResponses.maxWaitTime 
                                        
        }

    
    let onGetEntityIds (post: PostMessage) (actorMsg: ActorMessage) (getIds) (msgIds: string[] -> ActorMessage)=
        async { 
            let entityType = entityTypeName actorMsg
            do postDiscovered entityType [||]
            let! resp = getIds client
            return match resp with
                    | Choice1Of2 entityIds ->   let entityIds = entityIds |> Seq.map string |> Array.ofSeq
                                                
                                                sprintf "Found %i %s(s)" entityIds.Length entityType |> ActorMessage.Info |> log
                        
                                                entityIds   |> Seq.chunkBySize 10
                                                            |> Seq.map msgIds 
                                                            |> Array.ofSeq
                                                            |> Array.iter post
                                                entityIds   |> postDiscovered entityType
                                                
                                                TimeSpan.Zero
                    | Choice2Of2 errorResp ->   postbackEntityTypeError post actorMsg errorResp
                                                [errorResp] |> HttpResponses.maxWaitTime 
                                                
        }
        
        
    let onGetSystemIds (post: PostMessage) entityType =
        async {
            let! resp = Esi.systemIds client 

            return match resp with
                    | Choice1Of2 systemIds ->   let systemIds = systemIds 
                                                                    |> Seq.map string
                                                                    |> Array.ofSeq
                                                sprintf "Found %i %s(s)" systemIds.Length entityType |> ActorMessage.Info |> log
                                                systemIds |> Seq.map (string >> ActorMessage.SolarSystemId) |> Seq.iter post
                                                systemIds |> postDiscovered entityType
                                                
                                                TimeSpan.Zero
                    | Choice2Of2 errorResp ->   postbackEntityTypeError post ActorMessage.SolarSystems errorResp
                                                [errorResp] |> HttpResponses.maxWaitTime 
        }
        

    let onEntities (postBack) entityType (req: string -> HttpRequestMessage) (ids: string[]) =
        async {
            
            ids |> String.concatenate ", " |>  sprintf "Found %s %s" entityType |> ActorMessage.Info |> log

            let! meta = getMetadata (entityType, ids) 
            let idEtags = meta  |> Seq.reduceOptions  
                                |> Seq.map (fun m -> (m.id, m.etag) )
                                |> Map.ofSeq
            let getReq id = 
                match idEtags.TryFind id with
                | Some tag -> req id |> HttpRequests.etag tag
                | _ -> req id

            let reqs = ids  |> Array.map (fun id -> getReq id |> HttpResponses.response client 
                                                              |> Async.map (fun r -> (id,r) ) )

            let! resps = reqs |> Async.Parallel
            
            let okResps, failResps = resps |> Array.partition (fun (id,r) -> r.Status = HttpStatus.OK)
            
            let notModResps, failResps = failResps |> Array.partition (fun (id,resp) -> resp.Status = HttpStatus.OkNotModified)

            okResps     |> Seq.iter (fun (id,resp) -> 
                                        let etag = resp.ETag |> Option.map (fun e -> e.tag) |> Option.defaultValue ""
                                        (entityType, id, etag, resp.Message) |> ActorMessage.Entity |> writeEntity       
                                    )
            notModResps |> Seq.map (fun (id,_) -> (entityType, id)) 
                        |> Seq.iter ((ActorMessage.FinishedEntity >> crawlStatus) <--> (fun (e,id) -> sprintf "Skipping %s %s as not modified"  e id |> ActorMessage.Info |> log))

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
            // TODO: get etag; write only if modified
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
                                    try
                                        match inMsg with
                                        | ServerStatus ->           return! onGetServerStatus post inMsg 

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
                                        | MarketGroups ->           return! (onGetEntityIds post (inMsg) Esi.marketGroupIds ActorMessage.MarketGroupIds)
                                        | MarketGroupIds ids ->     return! (onEntities post (entityTypeName inMsg) Esi.marketGroupRequest ids)
                                        | NpcCorps ->               return! (onGetEntityIds post (inMsg) Esi.npcCorpIds ActorMessage.NpcCorpsIds)
                                        | NpcCorpsIds ids ->        return! (onEntities post (entityTypeName inMsg) Esi.corpRequest ids)
                                        | _ -> return TimeSpan.Zero
                                    with ex ->  ActorMessage.Exception ("", ex) |> (log <--> crawlStatus)
                                                post inMsg
                                                return TimeSpan.FromSeconds(1.)
                                  }
                                  
            return! getNext(nextWait)
        }
        
        getNext(TimeSpan.Zero)
    )
    
    do pipe.Error.Add(Actors.postException typeof<EsiCrawlerActor>.Name log)

    member __.Post(msg: ActorMessage) = pipe.Post msg