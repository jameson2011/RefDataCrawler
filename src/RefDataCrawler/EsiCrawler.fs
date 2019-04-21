namespace RefDataCrawler

open System
open System.Threading

type EsiCrawler(config: CrawlerConfig)=

    
    let seedingActorMessages config =
        
        let mandatoryMsgs = [| ActorMessage.ServerStatus |]
        let msgMaps = [| 
                        ((fun c -> c.crawlRegions), ActorMessage.Regions);
                        ((fun c -> c.crawlConstellations), ActorMessage.Constellations);
                        ((fun c -> c.crawlSystems), ActorMessage.SolarSystems);
                        ((fun c -> c.crawlGroups), ActorMessage.Groups);
                        ((fun c -> c.crawlCategories), ActorMessage.Categories);
                        ((fun c -> c.crawlTypes), ActorMessage.Types);
                        ((fun c -> c.crawlDogmaAttributes), ActorMessage.DogmaAttributes);
                        ((fun c -> c.crawlDogmaEffects), ActorMessage.DogmaEffects);
                        ((fun c -> c.crawlMarketGroups), ActorMessage.MarketGroups);
                      |]
        
        let msgs = msgMaps |> Seq.filter (fun (f,_) -> f config) 
                           |> Seq.map snd
                           |> Array.ofSeq

        let msgs =  if msgs.Length = 0 then msgMaps |> Array.map snd
                    else                    msgs
        
        [ mandatoryMsgs; msgs ] |> Array.concat

    
    let progress (status: CrawlProgress) = 
        let discovered = status.entityTypes |> Seq.map (fun et -> et.discovered) |> Seq.sum
        let completed = status.entityTypes |> Seq.map (fun et -> et.completed) |> Seq.sum
        let pc = if discovered <> 0 then (float completed / float discovered)  * 100.
                 else 0.
                 

        "\rDiscovered: " |> ConsoleUtils.white 
        discovered |> string |> ConsoleUtils.blue
        " Completed: " |> ConsoleUtils.white
        completed |> string |> ConsoleUtils.green
        " Errors: " |> ConsoleUtils.white
        status.errorCount |> string |> ConsoleUtils.red
        " : " |> ConsoleUtils.white
        pc |> sprintf "%.0f%%" |> ConsoleUtils.blue
        " complete.  " |> ConsoleUtils.white


    let completionReport (duration: TimeSpan) (status: CrawlProgress) = 
        
        let lines = seq {
                        yield ""
                        if status.isComplete then
                            yield "Complete"
                        else if status.isErrorLimited then
                            yield "Failed by error limiting"

                        yield duration.ToString() |> sprintf "Duration: %s"

                        yield! status.entityTypes 
                                |> Seq.map (fun et -> sprintf "Type: %s Discovered %i Completed %i" et.name et.discovered et.completed)

                        yield (status.entityTypes |> Seq.map (fun et -> et.discovered) |> Seq.sum |> sprintf "Total discovered: %i")

                        yield (status.entityTypes |> Seq.map (fun et -> et.completed) |> Seq.sum |> sprintf "Total completed: %i")

                        yield (status.errorCount |> sprintf "Errors: %i")
                    }
                             
        lines |> String.concatenate Environment.NewLine
        
        

    let rec waitComplete(config: CrawlerConfig) (crawlStatus: CrawlStatusActor) = 
        async {
            do! Async.Sleep(1000)

            let! status = crawlStatus.GetStatus()
                    
            if config.showProgressTicker then
                status |> progress 
                
            return! match status.isComplete, status.isErrorLimited with
                    | false, false -> waitComplete config crawlStatus
                    | false, true 
                    | true, _ ->      async { return status }               
            }

    let runCrawler (cts: CancellationTokenSource)=
        async {
            let start = System.DateTime.UtcNow
            

            let logger = LogPublishActor(config)
            let crawlStatus = CrawlStatusActor(logger.Post, config)
            let writer = EntityWriterActor(logger.Post, crawlStatus.Post, config)
            let crawler = CrawlerActor(logger.Post, crawlStatus.Post, writer.Post, writer.GetMetadata, config)
                        
            "Starting" |> ActorMessage.Info |> logger.Post
            
            config |> seedingActorMessages |> Seq.iter crawler.Post
            
            let! completedStatus = waitComplete config crawlStatus
            
            
            let msg,console =   if completedStatus.isComplete then  (ActorMessage.Info, ConsoleUtils.info)
                                else                                (ActorMessage.Error, ConsoleUtils.error)    
            completedStatus |> completionReport (System.DateTime.UtcNow - start)
                            |> (    if config.showProgressTicker then   ( ( msg >> logger.Post ) <--> console )
                                    else                                ( msg >> logger.Post ) )
            
            do! logger.Ping()
            
            cts.Cancel()
            
            return completedStatus.isComplete
        }

    let startCrawler () =        
        let cts = new System.Threading.CancellationTokenSource()
        
        let task = Async.StartAsTask(runCrawler cts)

        WaitHandle.WaitAny( [| cts.Token.WaitHandle |]) |> ignore 

        task.Result

    member this.Start() = startCrawler()


