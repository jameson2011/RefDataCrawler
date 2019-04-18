namespace RefDataCrawler

open System

type private StatsMap = Map<string, (int * int)>

type private CrawlStats = {
        errorCount:       int;
        entityTypeCounts: StatsMap;    
    }


type CrawlStatusActor(log: PostMessage)=
    
    
    let getCounts (stats: CrawlStats) entityType =
        if stats.entityTypeCounts.ContainsKey(entityType) then stats.entityTypeCounts.[entityType]
        else (0,0)
    
    let onDiscovered (stats: CrawlStats) entityType =
        let discovered,completed = getCounts stats entityType 

        let typeStats = (discovered+1, completed)

        { stats with entityTypeCounts = stats.entityTypeCounts.Add(entityType, typeStats) }
    
    let onFinished (stats: CrawlStats) entityType =
        let discovered,completed = getCounts stats entityType 
                   
        let typeStats = (discovered, completed + 1)
                
        { stats with entityTypeCounts = stats.entityTypeCounts.Add(entityType, typeStats) }
    
    let onError (stats: CrawlStats) =
        { stats with errorCount = stats.errorCount + 1}
        

    let entityTypeProgress(stats: StatsMap)=
        
        stats |> Seq.map (fun kvp -> { CrawlEntityTypeProgress.name = kvp.Key;
                                                  discovered = fst kvp.Value;
                                                  completed = snd kvp.Value })
              |> Array.ofSeq


    let isComplete (stats: CrawlEntityTypeProgress[]) =
        if stats.Length = 0 then
            false
        else
            let positives = stats       |> Seq.filter (fun s -> s.discovered > 0)
                                        |> Array.ofSeq
            let completions = positives |> Array.filter (fun s -> s.completed >= s.discovered)
                                                
            completions.Length = stats.Length

    let onGetStats (stats: CrawlStats) =
        
        let progress = entityTypeProgress stats.entityTypeCounts

        { CrawlProgress.entityTypes = progress; 
                        errorCount = stats.errorCount;
                        isComplete = isComplete progress }


    let pipe = MessageInbox.Start(fun inbox -> 
        
        let rec getNext(stats) = async {
               
            let! inMsg = inbox.Receive()

            let newStats = 
                    match inMsg with
                    | DiscoveredEntity (t,_) -> onDiscovered stats t
                    | FinishedEntity (t,_) ->   onFinished stats t
                    | Error _ ->                onError stats
                    | CrawlStatus ch ->         stats |> onGetStats |> ch.Reply 
                                                stats
                    | _ ->                      stats
                           
            return! getNext(newStats)
        }
        
        let stats = { CrawlStats.errorCount = 0; entityTypeCounts = Map.empty }
        getNext(stats)
    )

    do pipe.Error.Add(Actors.postException typeof<CrawlStatusActor>.Name log)

    member __.Post(msg: ActorMessage) = pipe.Post msg

    member __.GetStatus() =
        pipe.PostAndAsyncReply (fun ch -> ActorMessage.CrawlStatus ch)

