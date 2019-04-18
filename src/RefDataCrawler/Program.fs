namespace RefDataCrawler

open System

module Program =
    open System.Threading
    
    let private crawlerConfig (app: CommandLine.App) =
        { 
            CrawlerConfig.targetPath = CommandLine.getTargetFolderValue app;
                          crawlRegions = CommandLine.getRegionsValue app;
                          crawlConstellations = CommandLine.getConstellationsValue app;
                          crawlSystems = CommandLine.getSystemsValue app
        }

    let private seedingActorMessages config =
        seq {
                if config.crawlRegions then
                    yield ActorMessage.Regions

                if config.crawlConstellations then
                    yield ActorMessage.Constellations

                if config.crawlSystems then 
                    yield ActorMessage.SolarSystems
            } 
            |> Array.ofSeq 
            |> (fun xs -> if xs.Length = 0 then [| ActorMessage.Regions; ActorMessage.Constellations; ActorMessage.SolarSystems |] else xs )


    let private runCrawler (app: CommandLine.App) (cts: CancellationTokenSource)=
        async {
            let start = System.DateTime.UtcNow

            let config = crawlerConfig app

            let logger = LogPublishActor()
            let crawlStatus = CrawlStatusActor(logger.Post)
            let writer = EntityWriterActor(logger.Post, crawlStatus.Post, config)
            let crawler = CrawlerActor(logger.Post, crawlStatus.Post, writer.Post, config)
                        
            let rec checkComplete() = 
                async {
                    do! Async.Sleep(1000)

                    let! status = crawlStatus.GetStatus()
                    
                    return! match status.isComplete with
                            | false -> checkComplete()
                            | true -> async { return ignore 0 }               
                    }

            "Starting" |> ActorMessage.Info |> logger.Post
            
            config |> seedingActorMessages |> Seq.iter crawler.Post
            
            do! checkComplete()
            
            (System.DateTime.UtcNow - start).ToString() |> sprintf "Duration: %s" |> Console.Out.WriteLine
            
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
