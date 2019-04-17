namespace RefDataCrawler

open System

type private StatsMap = Map<string, (int * int)>


type CrawlStatusActor(log: PostMessage)=
    
    
    let getCounts (stats: StatsMap) entityType =
        if stats.ContainsKey(entityType) then stats.[entityType]
        else (0,0)
    
    let onDiscovered (stats: StatsMap) entityType id =
        let discovered,completed = getCounts stats entityType 

        let typeStats = (discovered+1, completed)

        stats.Add(entityType, typeStats)
    
    let onFinished (stats: StatsMap) entityType id =
        let discovered,completed = getCounts stats entityType 
                   
        let typeStats = (discovered, completed + 1)

        // TODO: if discovered > 0 & completed >= discovered ... perhaps we're done?
        
        stats.Add(entityType, typeStats)
    
    let onGetStats (stats: StatsMap) =
        
        stats |> Seq.map (fun kvp -> { CrawlStatus.name = kvp.Key;
                                                  discovered = fst kvp.Value;
                                                  completed = snd kvp.Value })
              |> Array.ofSeq


    let pipe = MessageInbox.Start(fun inbox -> 
        
        let rec getNext(stats) = async {
               
            let! inMsg = inbox.Receive()

            let newStats = 
                    match inMsg with
                    | DiscoveredEntity (t,id) ->    onDiscovered stats t id
                    | FinishedEntity (t,id) ->      onFinished stats t id
                    | CrawlStatus ch ->             stats |> onGetStats |> ch.Reply 
                                                    stats
                    | _ -> stats
                           
            return! getNext(newStats)
        }
        
        
        getNext(Map.empty)
    )

    do pipe.Error.Add(Actors.postException typeof<CrawlStatusActor>.Name log)

    member __.Post(msg: ActorMessage) = pipe.Post msg

    member __.GetStatus() =
        pipe.PostAndAsyncReply (fun ch -> ActorMessage.CrawlStatus ch)

