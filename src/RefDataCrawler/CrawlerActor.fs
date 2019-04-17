namespace RefDataCrawler

open System

type CrawlerActor(log: PostMessage, config: CrawlerConfig)=
    

    let pipe = MessageInbox.Start(fun inbox -> 
        let rec getNext(wait: TimeSpan) = async {
                
            let! inMsg = inbox.Receive()

            let! nextWait = async {
                                    match inMsg with
                                        | _ ->    
                                                return TimeSpan.Zero
                                    }
            return! getNext(nextWait)
        }
        
        getNext(TimeSpan.Zero)
    )

    do pipe.Error.Add(Actors.postException typeof<CrawlerActor>.Name log)

