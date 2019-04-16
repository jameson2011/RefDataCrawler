namespace RefDataCrawler

open System

module Program =
    
    let private runCrawler =
        async {
            let path = "v1/universe/regions/"
            let client = HttpRequests.httpClient()
            let req = HttpRoutes.url path
                        |> HttpRequests.get
                        
            let! resp = req |> HttpResponses.getData client

            let etag = match resp.Status with
                        | HttpStatus.OK  -> resp.ETag
                        | _ -> None

            let req = HttpRoutes.url path
                        |> HttpRequests.get
                        |> HttpRequests.etag etag.Value.tag
            let! resp2 = req |> HttpResponses.getData client


            // get the ESI server status
            // if different -> continue

            return 0 |> ignore
        }

    let private startCrawler (app: CommandLine.App) =        
        let cts = new System.Threading.CancellationTokenSource()

        Async.Start(runCrawler, cts.Token)
        
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
