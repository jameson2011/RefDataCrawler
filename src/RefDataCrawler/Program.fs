namespace RefDataCrawler

open System

module Program =
    
    let private crawlerConfig (app: CommandLine.App) =
        { 
            CrawlerConfig.targetPath = CommandLine.getTargetFolderValue app 
        }

    let private runCrawler (app: CommandLine.App)=
        async {
            let start = System.DateTime.UtcNow

            let config = crawlerConfig app

            let client = HttpRequests.httpClient()
            
            // TODO: get the last known server version. If different to the current server version, start...
            let! serverStatus = Esi.serverStatus client
            // get the ESI server status
            // if different -> continue



            let! regionIds = Esi.regionIds client
            // TODO: 

                        
            let! systemIDs = (client |> Esi.systemIds) 

            sprintf "Found %i systems" systemIDs.Length |> Console.Out.WriteLine

            let systemIDs = systemIDs |> Seq.map (fun i -> i.ToString()) |> List.ofSeq

            
            let rec getSystems ids systems =
                async {
                    if ids = [] then
                        return systems
                    else 
                        let id::t = ids
                        
                        
                        let! s = id |> Esi.system client
                        
                        let systems = match s with
                                        | Some sys -> sys.Name |> sprintf "Got system %s %s" id |> Console.Out.WriteLine
                                                      (sys :: systems)
                                        | None -> systems
                        
                        return! getSystems t systems
                        
                }

            let! systems = getSystems systemIDs []


            let finish = System.DateTime.UtcNow
            let duration = finish - start

            duration.TotalSeconds |> sprintf "Duration: %fsecs" |> Console.Out.WriteLine

            return 0 |> ignore
        }

    let private startCrawler (app: CommandLine.App) =        
        let cts = new System.Threading.CancellationTokenSource()

        Async.Start(runCrawler app, cts.Token)
        
        Console.WriteLine("Hit Return to quit")
        Console.ReadLine() |> ignore 

        //"Shutting down..." |> logInfo

        cts.Cancel()


        true

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
