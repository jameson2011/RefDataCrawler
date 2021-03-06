﻿namespace RefDataCrawler

module CommandLine=

    open System
    open Microsoft.Extensions
    
    type App = CommandLineUtils.CommandLineApplication
    type CmdOption = CommandLineUtils.CommandOption
    type Command = App -> bool

    let private sourceFolderArg = "source"
    let private targetFolderArg = "dest"
    let private regionsArg = "regions"
    let private constellationsArg = "constellations"
    let private systemsArg = "systems"
    let private groupsArg = "groups"
    let private categoriesArg = "categories"
    let private typesArg = "types"
    let private dogmaAttributesArg = "dogma_attributes"
    let private dogmaEffectsArg = "dogma_effects"
    let private marketGroupsArg = "market_groups"
    let private npcCorpsArg = "npccorps"
    let private verboseArg = "verbose"
    let private progressTickerArg = "progress"
    let private errorLimitArg = "maxerrors"

    let private longestArg = 
        [ targetFolderArg; regionsArg; constellationsArg; systemsArg; 
            groupsArg; categoriesArg; typesArg; dogmaAttributesArg; dogmaEffectsArg;
            npcCorpsArg;
            verboseArg; progressTickerArg; errorLimitArg; ]
        |> Seq.map String.length
        |> Seq.max

    let addSingleOption (name: string) fullName desc (app:App)=
        let pad = String(' ', max 0 (longestArg - name.Length) )
        let tag = sprintf "-%s%s | --%s" name pad fullName
        app.Option(tag, desc,CommandLineUtils.CommandOptionType.SingleValue) |> ignore
        app

    let addSwitchOption (name: string) fullName desc (app:App)=
        let pad = String(' ', max 0 (longestArg - name.Length) )
        let tag = sprintf "-%s%s | --%s" name pad fullName
        app.Option(tag, desc,CommandLineUtils.CommandOptionType.NoValue) |> ignore
        app

    
    let getOption shortName (app: App)  =
        app.Options
        |> Seq.tryFind (fun o -> o.ShortName = shortName)
    
    let getStringOption shortName (app:App) =
        match (getOption shortName app) with
        | Some x when x.HasValue() -> x.Value() |> Some
        | _ -> None 

    let getIntOption shortName (app:App) =
        match (getOption shortName app) with
        | Some x when x.HasValue() -> x.Value() |> System.Int32.Parse |> Some
        | _ -> None 


    let getSwitchOption (shortName: string) (app:App)=
        match (getOption shortName app) with
        | Some x -> x.Values.Count > 0
        | _ -> false


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

    let addSourceFolderArg =                addSingleOption sourceFolderArg sourceFolderArg "The source folder"
    let getSourceFolderArg app =            getStringOption sourceFolderArg app 

    let addTargetFolderArg =                addSingleOption targetFolderArg targetFolderArg "The target folder"
    let getTargetFolderValue app =          getStringOption targetFolderArg app 

    let addRegionsArg =                     addSwitchOption regionsArg regionsArg "Crawl Regions"
    let getRegionsValue app =               getSwitchOption regionsArg app
    
    let addConstellationsArg =              addSwitchOption constellationsArg constellationsArg "Crawl Constellations"
    let getConstellationsValue app =        getSwitchOption constellationsArg app
    
    let addSystemsArg =                     addSwitchOption systemsArg systemsArg "Crawl Systems"
    let getSystemsValue app =               getSwitchOption systemsArg app
    
    let addGroupsArg =                      addSwitchOption groupsArg groupsArg "Crawl Groups"
    let getGroupsValue app =                getSwitchOption groupsArg app

    let addCategoriesArg =                  addSwitchOption categoriesArg categoriesArg "Crawl Categories"
    let getCategoriesValue app =            getSwitchOption categoriesArg app

    let addTypesArg =                       addSwitchOption typesArg typesArg "Crawl Types"
    let getTypesValue app =                 getSwitchOption typesArg app

    let addDogmaAttributesArg =             addSwitchOption dogmaAttributesArg dogmaAttributesArg "Crawl Dogma Attributes"
    let getDogmaAttributesValue app =       getSwitchOption dogmaAttributesArg app

    let addDogmaEffectsArg =                addSwitchOption dogmaEffectsArg dogmaEffectsArg "Crawl Dogma Effects"
    let getDogmaEffectsValue app =          getSwitchOption dogmaEffectsArg app

    let addMarketGroupsArg =                addSwitchOption marketGroupsArg marketGroupsArg "Crawl Market Groups"
    let getMarketGroupsValue app =          getSwitchOption marketGroupsArg app

    let addNpcCorpsArg =                    addSwitchOption npcCorpsArg npcCorpsArg "Crawl NPC Corps"
    let getNpcCorpsValue app =              getSwitchOption npcCorpsArg app

    let addVerboseArg =                     addSwitchOption verboseArg verboseArg "Verbose logging"
    let getVerboseValue app =               getSwitchOption verboseArg app
    
    let addProgressTickerArg =              addSwitchOption progressTickerArg progressTickerArg "Show progress ticker"
    let getProgressTickerValue app =        getSwitchOption progressTickerArg app
    
    let addMaxErrorsArg =                   addSingleOption errorLimitArg errorLimitArg "Maximum errors tolerated"
    let getMaxErrorsValue app =             getIntOption errorLimitArg app |> Option.defaultValue 0

    let addSourceUriArg =                   addSingleOption sourceFolderArg sourceFolderArg "The source Uri"
    let getSourceUriArg app =               getStringOption sourceFolderArg app |> Option.defaultValue "https://eve-static-data-export.s3-eu-west-1.amazonaws.com/tranquility/sde.zip"

    let createApp() =
        let app = new App(false)
        app.Name <- "RefDataCrawler"
        app.Description <- "ESI reference data crawler"
        app

    let private composeAppPipe(f: App -> App) = new System.Action<App>(f >> setHelp >> ignore)
    

    let addCrawl cmd (app: App) =
        let f = setDesc "Run the crawler" 
                        >> addTargetFolderArg
                        >> addRegionsArg
                        >> addConstellationsArg
                        >> addSystemsArg
                        >> addGroupsArg
                        >> addCategoriesArg
                        >> addTypesArg
                        >> addDogmaAttributesArg
                        >> addDogmaEffectsArg
                        >> addMarketGroupsArg
                        >> addNpcCorpsArg
                        >> addVerboseArg
                        >> addProgressTickerArg
                        >> addMaxErrorsArg
                        >> setAction cmd
        app.Command("crawl", (composeAppPipe f)) |> ignore
        app

    let addGenerate cmd (app: App) =
        let f = setDesc "Generate code"
                        >> addSourceFolderArg
                        >> addTargetFolderArg
                        >> setAction cmd
        app.Command("gen", composeAppPipe f) |> ignore
        app

    let addCrawlSde cmd (app: App) =
        let f = setDesc "Crawl SDE"
                    >> addTargetFolderArg
                    >> addSourceUriArg
                    >> addVerboseArg
                    >> setAction cmd
        app.Command("sde", composeAppPipe f) |> ignore
        app