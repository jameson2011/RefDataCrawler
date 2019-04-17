namespace RefDataCrawler

open System
open System.Net.Http

type CrawlerActor(log: PostMessage, write: PostMessage, config: CrawlerConfig)=
    
    let client = HttpRequests.httpClient()

    let safe defaultValue map =
        try
            map()
        with
        | _ -> defaultValue
    
    let onGetRegionIds (post: PostMessage)=
        async {
            let! regionIds = Esi.regionIds client
            
            sprintf "Found %i regions" regionIds.Length |> ActorMessage.Info |> log
            regionIds |> Seq.map (string >> ActorMessage.RegionId) |> Seq.iter post
            
            return TimeSpan.Zero
        }

    let onGetConstellationIds (post: PostMessage)=
        async {
            let! constellationIds = Esi.constellationIds client
            
            sprintf "Found %i constellations" constellationIds.Length |> ActorMessage.Info |> log
            constellationIds |> Seq.map (string >> ActorMessage.ConstellationId) |> Seq.iter post
            
            return TimeSpan.Zero
        }

    let onGetSystemIds (post: PostMessage) =
        async {
            let! systemIDs = (client |> Esi.systemIds) 

            sprintf "Found %i systems" systemIDs.Length |> ActorMessage.Info |> log
            systemIDs   //|> Seq.filter (fun s -> s = 30005003) // TODO: temporary
                        |> Seq.map (string >> ActorMessage.SystemId) 
                        |> Seq.iter post

            return TimeSpan.Zero
        }

    let onSystemId (post: PostMessage) id = 
        async {
            id |> sprintf "Found system %s" |> ActorMessage.Info |> log

            let! resp = id  |> Esi.systemRequest
                            |> HttpResponses.response client
                        
            return 
                if resp.Status = HttpStatus.OK then
                    
                    let system = Esi.toSolarSystem resp
                    
                    let etag = resp.ETag |> Option.map (fun e -> e.tag) |> Option.defaultValue ""
                    ("system", id, etag, resp.Message) |> ActorMessage.Entity |> write
                    
                    
                    // get the stations, planets, moons, belts, etc...
                    let planetIds = system.Planets |> Seq.map (fun x -> string x.PlanetId |> ActorMessage.PlanetId) |> List.ofSeq
                    
                    let starIds = (fun () -> [ string system.StarId ]) |> safe []
                                    |> List.map ActorMessage.StarId

                    let moonIds = (fun () -> system.Planets) |> safe [||]
                                    |> Seq.collect (fun p -> p.Moons) |> Seq.map (string >> ActorMessage.MoonId) |> List.ofSeq

                    let beltIds = (fun () -> system.Planets) |> safe [||]
                                    |> Seq.collect (fun p -> p.AsteroidBelts) |> Seq.map (string >> ActorMessage.AsteroidBeltId) |> List.ofSeq

                    let stationIds = (fun () -> system.Stations) |> safe [||]
                                        |> Seq.map (string >> ActorMessage.StationId)|> List.ofSeq

                    let stargateIds = (fun () -> system.Stargates) |> safe [||]
                                        |> Seq.map (string >> ActorMessage.StargateId) |> List.ofSeq
                
                    planetIds @ starIds @ moonIds @ beltIds @ stationIds @ stargateIds
                        |> Seq.iter post

                    TimeSpan.Zero
                else
                    // TODO: error! what if "OkNotModified"?
                    // TODO: termination?
                    match resp.Retry with
                    | Some ts -> ts
                    | _ -> TimeSpan.Zero
                
        }

    let onEntity entityType (req: string -> HttpRequestMessage) id=
        async {
            sprintf "Found %s %s" entityType id |> ActorMessage.Info |> log

            let! resp = id  |> req
                            |> HttpResponses.response client
            return 
                if resp.Status = HttpStatus.OK then
                    
                    let etag = resp.ETag |> Option.map (fun e -> e.tag) |> Option.defaultValue ""
                    (entityType, id, etag, resp.Message) |> ActorMessage.Entity |> write

                    TimeSpan.Zero

                else
                    match resp.Retry with
                    | Some ts -> ts
                    | _ -> TimeSpan.Zero
        }
    
        

    let pipe = MessageInbox.Start(fun inbox -> 
        
        let post = inbox.Post
        let rec getNext(wait: TimeSpan) = async {
                
            // TODO: pause if wait nonzero
            // 
            let! inMsg = inbox.Receive()

            let! nextWait = async {
                                    match inMsg with
                                    | RegionIds ->          return! (onGetRegionIds post)
                                    | RegionId id ->        return! (onEntity "region" Esi.regionRequest id)

                                    | ConstellationIds ->   return! (onGetConstellationIds post)
                                    | ConstellationId id -> return! (onEntity "constellation" Esi.constellationRequest id)

                                    | SystemIds ->          return! (onGetSystemIds post)
                                    | SystemId id ->        return! (onSystemId post id)
                                            
                                    | PlanetId id ->        return! (onEntity "planet" Esi.planetRequest id)
                                    | AsteroidBeltId id ->  return! (onEntity "belt" Esi.asteroidBeltRequest id)
                                    | MoonId id ->          return! (onEntity "moon" Esi.moonRequest id)
                                    | StarId id ->          return! (onEntity "star" Esi.starRequest id)
                                    | StationId id ->       return! (onEntity "station" Esi.stationRequest id)
                                    | StargateId id ->      return! (onEntity "stargate" Esi.stargateRequest id)

                                    | _ -> return TimeSpan.Zero
                                    }
            return! getNext(nextWait)
        }
        
        getNext(TimeSpan.Zero)
    )

    do pipe.Error.Add(Actors.postException typeof<CrawlerActor>.Name log)

    member __.Post(msg: ActorMessage) = pipe.Post msg