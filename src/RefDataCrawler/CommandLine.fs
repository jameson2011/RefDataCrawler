namespace RefDataCrawler

module CommandLine=

    open System
    open Microsoft.Extensions
    
    type App = CommandLineUtils.CommandLineApplication
    type CmdOption = CommandLineUtils.CommandOption
    type Command = App -> bool

    let private targetFolderArg = "dest"
    
    
    let addSingleOption (name: string) fullName desc (app:App)=
        let pad = String(' ', max 0 (4 - name.Length) )
        let tag = sprintf "-%s%s | --%s" name pad fullName
        app.Option(tag, desc,CommandLineUtils.CommandOptionType.SingleValue) |> ignore
        app

    
    let getOption shortName (app: App)  =
        app.Options
        |> Seq.tryFind (fun o -> o.ShortName = shortName)
    
    let getStringOption shortName (app:App) =
        match (getOption shortName app) with
        | Some x when x.HasValue() -> x.Value() |> Some
        | _ -> None 

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

    let addTargetFolderArg =              addSingleOption targetFolderArg targetFolderArg "The target folder"
    let getTargetFolderValue app =        getStringOption targetFolderArg app |> Option.defaultValue CrawlerConfig.TargetPathDefault


    let createApp() =
        let app = new App(false)
        app.Name <- "RefDataCrawler"
        app.Description <- "ESI reference data crawler"
        app

    let private composeAppPipe(f: App -> App) = new System.Action<App>(f >> setHelp >> ignore)
    

    let addRun cmd (app: App) =
        let f = setDesc "Run the crawler" 
                        >> addTargetFolderArg
                        >> setAction cmd
        app.Command("run", (composeAppPipe f)) |> ignore
        app