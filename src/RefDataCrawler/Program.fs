namespace RefDataCrawler

open System

module Program =
    open System.Threading
    
    let private crawlerConfig (app: CommandLine.App) =
        { 
            CrawlerConfig.targetPath = CommandLine.getTargetFolderValue app 
        }

    let private runCrawler (app: CommandLine.App) (cts: CancellationTokenSource)=
        async {
            let start = System.DateTime.UtcNow

            let config = crawlerConfig app

            let logger = LogPublishActor()

            "Starting" |> ActorMessage.Info |> logger.Post
            
                        
            let crawlStatus = CrawlStatusActor(logger.Post)
            let writer = EntityWriterActor(logger.Post, crawlStatus.Post, config)
            let crawler = CrawlerActor(logger.Post, crawlStatus.Post, writer.Post, config)
            
            // TODO: subject to cmdline args
            [ 
                //ActorMessage.Regions; 
                //ActorMessage.Constellations; // TODO: temp
                ActorMessage.SolarSystems 
            ] |> Seq.iter crawler.Post
                        

            let isComplete() =
                async {
                    let! status = crawlStatus.GetStatus()

                    if status.Length = 0 then
                        return false
                    else
                        let positives = status      |> Seq.filter (fun s -> s.discovered > 0)
                                                    |> Array.ofSeq
                        let completions = positives |> Array.filter (fun s -> s.completed >= s.discovered)
                                                
                        // TODO: include accounting for error
                        return completions.Length = status.Length
                }
            
            let rec checkComplete() = 
                async {
                    do! Async.Sleep(2000)

                    let! isComplete = isComplete()

                    return! match isComplete with
                            | false -> checkComplete()
                            | true -> async { return ignore 0 }
                            
                    }

            do! checkComplete()
            
            let finish = System.DateTime.UtcNow
            let duration = finish - start

            duration.ToString() |> sprintf "Duration: %s" |> Console.Out.WriteLine
            
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
