namespace RefDataCrawler

open System
open System.Net.Http

type CrawlerActor(log: PostMessage, crawlStatus: PostMessage, writeEntity: PostMessage, config: CrawlerConfig)=
    
    let client = HttpRequests.httpClient()

    let safe defaultValue map =
        try
            map()
        with
        | _ -> defaultValue
    
    let postDiscovered entityType ids =
        ids 
            |> Seq.map string
            |> Seq.map (fun s -> (entityType, s)) |> Seq.map ActorMessage.DiscoveredEntity |> Seq.iter crawlStatus

    let entityType msg =
        match msg with
        | RegionId _ ->        "region"
        | ConstellationId _ -> "constellation"
        | SystemId _ ->        "system"
        | PlanetIds _ ->        "planet"
        | AsteroidBeltIds _ ->  "belt"
        | MoonIds _ ->          "moon"
        | StarIds _ ->          "star"
        | StationIds _ ->       "station"
        | StargateIds _ ->      "stargate"
        | _ -> ""

    let entityTypeId msg =
        match msg with
        | RegionId id 
        | ConstellationId id 
        | SystemId id 
        | PlanetIds id 
        | AsteroidBeltIds id 
        | MoonIds id 
        | StarIds id 
        | StationIds id 
        | StargateIds id ->      id
        | _ -> ""


    let onGetRegionIds (post: PostMessage)=
        async {
            let! regionIds = Esi.regionIds client
            
            sprintf "Found %i regions" regionIds.Length |> ActorMessage.Info |> log
            regionIds |> Seq.map (string >> ActorMessage.RegionId) |> Seq.iter post
            regionIds |> postDiscovered "region"

            return TimeSpan.Zero
        }

    let onGetConstellationIds (post: PostMessage)=
        async {
            let! constellationIds = Esi.constellationIds client
            
            sprintf "Found %i constellations" constellationIds.Length |> ActorMessage.Info |> log
            constellationIds |> Seq.map (string >> ActorMessage.ConstellationId) |> Seq.iter post
            constellationIds |> postDiscovered "constellation"

            return TimeSpan.Zero
        }

    let onGetSystemIds (post: PostMessage) =
        async {
            let! systemIds = (client |> Esi.systemIds)

            let systemIds = systemIds 
                                //|> Seq.filter (fun s -> s = 30005003) // TODO: temporary
                                |> Array.ofSeq
            sprintf "Found %i systems" systemIds.Length |> ActorMessage.Info |> log
            systemIds |> Seq.map (string >> ActorMessage.SystemId) |> Seq.iter post
            systemIds |> postDiscovered "system"

            return TimeSpan.Zero
        }

    let onEntity entityType (req: string -> HttpRequestMessage) id=
        async {
            sprintf "Found %s %s" entityType id |> ActorMessage.Info |> log

            let! resp = id  |> req
                            |> HttpResponses.response client
            return 
                if resp.Status = HttpStatus.OK then
                    
                    let etag = resp.ETag |> Option.map (fun e -> e.tag) |> Option.defaultValue ""
                    (entityType, id, etag, resp.Message) |> ActorMessage.Entity |> writeEntity

                    TimeSpan.Zero

                else
                    // TODOK: on error, post back

                    match resp.Retry with
                    | Some ts -> ts
                    | _ -> TimeSpan.Zero
        }
    
        
    let onSystemId (postBack: PostMessage) id = 
        async {
            id |> sprintf "Found system %s" |> ActorMessage.Info |> log

            let! resp = id  |> Esi.systemRequest
                            |> HttpResponses.response client
                        
            return 
                if resp.Status = HttpStatus.OK then
                    
                    let system = Esi.toSolarSystem resp
                    
                    let etag = resp.ETag |> Option.map (fun e -> e.tag) |> Option.defaultValue ""
                    ("system", id, etag, resp.Message) |> ActorMessage.Entity |> writeEntity
                    
                    
                    // get the stations, planets, moons, belts, etc...
                    let planetIds = (fun () -> system.Planets) |> safe [||]
                                        |> Seq.map (fun x -> string x.PlanetId |> ActorMessage.PlanetIds) |> List.ofSeq
                    
                    let starIds = (fun () -> [ string system.StarId ]) |> safe []
                                    |> List.map ActorMessage.StarIds

                    let moonIds = (fun () -> system.Planets) |> safe [||]
                                    |> Seq.collect (fun p -> p.Moons) |> Seq.map (string >> ActorMessage.MoonIds) |> List.ofSeq

                    let beltIds = (fun () -> system.Planets) |> safe [||]
                                    |> Seq.collect (fun p -> p.AsteroidBelts) |> Seq.map (string >> ActorMessage.AsteroidBeltIds) |> List.ofSeq

                    let stationIds = (fun () -> system.Stations) |> safe [||]
                                        |> Seq.map (string >> ActorMessage.StationIds)|> List.ofSeq

                    let stargateIds = (fun () -> system.Stargates) |> safe [||]
                                        |> Seq.map (string >> ActorMessage.StargateIds) |> List.ofSeq
                
                    let entities = planetIds @ starIds @ moonIds @ beltIds @ stationIds @ stargateIds
                        
                    entities |> Seq.iter postBack
                    entities |> Seq.map (fun e -> (entityType e, entityTypeId e)) 
                             |> Seq.map ActorMessage.DiscoveredEntity 
                             |> Seq.iter crawlStatus

                    TimeSpan.Zero
                else
                    // TODO: notify status of error, so tallies match... or put this back onto the queue. May want to think about retry attempts...
                    id |> ActorMessage.SystemId |> postBack

                    // TODO: error! what if "OkNotModified"?
                    // TODO: termination?
                    match resp.Retry with
                    | Some ts -> ts
                    | _ -> TimeSpan.Zero
                
        }


    let pipe = MessageInbox.Start(fun inbox -> 
        
        let post = inbox.Post
        let rec getNext(wait: TimeSpan) = async {
               
            if(wait.TotalMilliseconds > 0.) then
              wait.TotalMilliseconds |> sprintf "Throttling back for %f msec..." |> ActorMessage.Info |> log
              do! Async.Sleep(int wait.TotalMilliseconds)
            
            let! inMsg = inbox.Receive()

            let! nextWait = async {
                                    match inMsg with
                                    | RegionIds ->          return! (onGetRegionIds post)
                                    | RegionId id ->        return! (onEntity (entityType inMsg) Esi.regionRequest id)

                                    | ConstellationIds ->   return! (onGetConstellationIds post)
                                    | ConstellationId id -> return! (onEntity (entityType inMsg) Esi.constellationRequest id)

                                    | SystemIds ->          return! (onGetSystemIds post)
                                    | SystemId id ->        return! (onSystemId post id)
                                            
                                    | PlanetIds ids ->        return! (onEntity (entityType inMsg) Esi.planetRequest ids)
                                    | AsteroidBeltIds ids ->  return! (onEntity (entityType inMsg) Esi.asteroidBeltRequest ids)
                                    | MoonIds ids ->          return! (onEntity (entityType inMsg) Esi.moonRequest ids)
                                    | StarIds ids ->          return! (onEntity (entityType inMsg) Esi.starRequest ids)
                                    | StationIds ids ->       return! (onEntity (entityType inMsg) Esi.stationRequest ids)
                                    | StargateIds ids ->      return! (onEntity (entityType inMsg) Esi.stargateRequest ids)

                                    | _ -> return TimeSpan.Zero
                                    }
            return! getNext(nextWait)
        }
        
        getNext(TimeSpan.Zero)
    )

    do pipe.Error.Add(Actors.postException typeof<CrawlerActor>.Name log)

    member __.Post(msg: ActorMessage) = pipe.Post msg