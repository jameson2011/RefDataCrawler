namespace RefDataCrawler

module CommandLine=

    open Microsoft.Extensions
    
    type App = CommandLineUtils.CommandLineApplication
    type CmdOption = CommandLineUtils.CommandOption
    type Command = App -> bool


    
    let setHelp(app: App) =
        app.HelpOption("-? | -h | --help") |> ignore
        app
           
    
    let setAction (fn: Command) (app: App) =
        let act = fun () ->     if fn(app) then 0
                                else 2
        app.OnExecute(act) 
        app

    let setDesc desc (app: App) =
        app.Description <- desc
        app


    let createApp() =
        let app = new App(false)
        app.Name <- "RefDataCrawler"
        app.Description <- "ESI reference data crawler"
        app

    let private composeAppPipe(f: App -> App) = new System.Action<App>(f >> setHelp >> ignore)
    

    let addRun cmd (app: App) =
        let f = setDesc "Run the crawler" 
                                    (*
                                    >> addKillSourceUriArg
                                    >> addMongoServerArg >> addMongoDbArg >> addMongoKillsCollectionArg >> addMongoSessionsCollectionArg
                                    >> addMongoUserArg >> addMongoPasswordArg 
                                    >> addWebServerPortArg
                                    >> addLiveBufferSizeArg
                                    >> addNoCacheArg
                                    >> addSessionTimeoutArg
                                    *)
                                    >> setAction cmd
        app.Command("run", (composeAppPipe f)) |> ignore
        app