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
        }
        

    let private seedingActorMessages config =
        // TODO: tidy up...
        seq {
                if config.crawlRegions then
                    yield ActorMessage.Regions

                if config.crawlConstellations then
                    yield ActorMessage.Constellations

                if config.crawlSystems then 
                    yield ActorMessage.SolarSystems

                if config.crawlGroups then  
                    yield ActorMessage.Groups

                if config.crawlCategories then
                    yield ActorMessage.Categories

                if config.crawlTypes then
                    yield ActorMessage.Types

                if config.crawlDogmaAttributes then
                    yield ActorMessage.DogmaAttributes

                if config.crawlDogmaEffects then    
                    yield ActorMessage.DogmaEffects
            } 
            |> Array.ofSeq 
            |> (fun xs ->   if xs.Length = 0 then 
                                [|  ActorMessage.Regions; ActorMessage.Constellations; ActorMessage.SolarSystems; 
                                    ActorMessage.Groups; ActorMessage.Categories; ActorMessage.Types; 
                                    ActorMessage.DogmaAttributes; ActorMessage.DogmaEffects |] 
                            else xs )

    
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


    let private progressReport (status: CrawlProgress) = 
        
        let result = status.entityTypes |> Seq.map (fun et -> sprintf "Type: %s Discovered %i Completed %i" et.name et.discovered et.completed)
                                        |> String.concatenate Environment.NewLine

        result
        

    let rec private checkComplete(config: CrawlerConfig) (crawlStatus: CrawlStatusActor) = 
        async {
            do! Async.Sleep(1000)

            let! status = crawlStatus.GetStatus()
                    
            if config.showProgressTicker then
                status |> progress 
                
            return! match status.isComplete with
                    | false -> checkComplete config crawlStatus
                    | true -> async { return ignore 0 }               
            }

    let private runCrawler (app: CommandLine.App) (cts: CancellationTokenSource)=
        async {
            let start = System.DateTime.UtcNow

            let config = crawlerConfig app

            let logger = LogPublishActor(config)
            let crawlStatus = CrawlStatusActor(logger.Post)
            let writer = EntityWriterActor(logger.Post, crawlStatus.Post, config)
            let crawler = CrawlerActor(logger.Post, crawlStatus.Post, writer.Post, config)
                        
            "Starting" |> ActorMessage.Info |> logger.Post
            
            config |> seedingActorMessages |> Seq.iter crawler.Post
            
            do! checkComplete config crawlStatus
            
            let! status = crawlStatus.GetStatus()
            status |> progressReport |> ActorMessage.Info |> logger.Post
            
            // TODO: wait for logger to clear...

            (System.DateTime.UtcNow - start).ToString() |> sprintf "\r\nDuration: %s" |> Console.Out.WriteLine
            
            cts.Cancel()
            
            return 0 |> ignore
        }

    let private startCrawler (app: CommandLine.App) =        
        let cts = new System.Threading.CancellationTokenSource()

        Async.Start(runCrawler app cts)
        
        WaitHandle.WaitAny( [| cts.Token.WaitHandle |]) > 0
        

    let private createAppTemplate()=
        let app = CommandLine.createApp()
                    |> CommandLine.addRun startCrawler
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
