namespace RefDataCrawler

open System

type private StatsMap = Map<string, (int * int)>


type CrawlStatusActor(log: PostMessage)=
    
    
    let getCounts (stats: StatsMap) entityType =
        if stats.ContainsKey(entityType) then stats.[entityType]
        else (0,0)
    
    let onDiscovered (stats: StatsMap) entityType id =
        let item = getCounts stats entityType 

        let typeStats = ((fst item)+1, (snd item))

        stats.Add(entityType, typeStats)
    
    let onFinished (stats: StatsMap) entityType id =
        let item = getCounts stats entityType 
                   
        let typeStats = ((fst item), (snd item) + 1)

        // TODO: if fst Item > 0 & fst Item = snd Item... perhaps we're done?
        
        stats.Add(entityType, typeStats)
    
    let pipe = MessageInbox.Start(fun inbox -> 
        
        let rec getNext(stats) = async {
               
            let! inMsg = inbox.Receive()

            let newStats = 
                    match inMsg with
                    | DiscoveredEntity (t,id) -> onDiscovered stats t id
                    | FinishedEntity (t,id) ->  onFinished stats t id
                    | CrawlStatus -> stats// TODO: post back results
                    // TODO: errors?
                    | _ -> stats
                           
            return! getNext(newStats)
        }
        
        
        getNext(Map.empty)
    )

    do pipe.Error.Add(Actors.postException typeof<CrawlStatusActor>.Name log)

    member __.Post(msg: ActorMessage) = pipe.Post msg

