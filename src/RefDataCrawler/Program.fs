namespace RefDataCrawler

open System

module Program =
    open System.Threading
    
    let private crawlerConfig (app: CommandLine.App) =
        let verboseLogging = CommandLine.getVerboseValue app;
        let showProgressTicker = CommandLine.getProgressTickerValue app;
        
        let verboseLogging = if showProgressTicker then false
                             else true

        { CrawlerConfig.targetPath = CommandLine.getTargetFolderValue app;
                          crawlRegions = CommandLine.getRegionsValue app;
                          crawlConstellations = CommandLine.getConstellationsValue app;
                          crawlSystems = CommandLine.getSystemsValue app;
                          crawlGroups = CommandLine.getGroupsValue app;
                          crawlCategories = CommandLine.getCategoriesValue app;
                          crawlTypes = CommandLine.getTypesValue app;
                          crawlDogmaAttributes = CommandLine.getDogmaAttributesValue app;
                          crawlDogmaEffects = CommandLine.getDogmaEffectsValue app;
                          verboseLogging = verboseLogging;
                          showProgressTicker = showProgressTicker;
                          maxErrors = CommandLine.getMaxErrorsValue app;
        }
        
    
    let private seedingActorMessages config =
        
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
                      |]
        
        let msgs = msgMaps |> Seq.filter (fun (f,_) -> f config) 
                           |> Seq.map snd
                           |> Array.ofSeq

        let msgs =  if msgs.Length = 0 then msgMaps |> Array.map snd
                    else                    msgs
        
        [ mandatoryMsgs; msgs ] |> Array.concat

    
    let private progress (status: CrawlProgress) = 
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


    let private completionReport (duration: TimeSpan) (status: CrawlProgress) = 
        
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
        
        

    let rec private waitComplete(config: CrawlerConfig) (crawlStatus: CrawlStatusActor) = 
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

    let private runCrawler (app: CommandLine.App) (cts: CancellationTokenSource)=
        async {
            let start = System.DateTime.UtcNow

            let config = crawlerConfig app

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
            
            return 0 |> ignore
        }

    let private startCrawler (app: CommandLine.App) =        
        let cts = new System.Threading.CancellationTokenSource()

        Async.Start(runCrawler app cts)
        
        WaitHandle.WaitAny( [| cts.Token.WaitHandle |]) > 0
        

    let private createAppTemplate()=
        let app = CommandLine.createApp()
                    |> CommandLine.addCrawl startCrawler
                    |> CommandLine.setHelp
    
        app.OnExecute(fun () -> app.ShowHelp()
                                0)
        app

    [<EntryPoint>]
    let main argv =
        let app = createAppTemplate()
    
        app.OnExecute(fun () -> app.ShowHelp()
                                0)

        try
                app.Execute(argv)

        with
        | ex -> ConsoleUtils.error ex.Message   
                2
