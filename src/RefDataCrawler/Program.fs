namespace RefDataCrawler

open System

module Program =

    let private runCrawler =
        async {
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
