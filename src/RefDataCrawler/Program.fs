namespace RefDataCrawler


module Program =
    
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
        
        
    let private startCrawler (app: CommandLine.App) =        
        let config = crawlerConfig app

        let crawler = new EsiCrawler(config)

        crawler.Start()

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
                let rc = app.Execute(argv)
                rc
        with
        | ex -> ConsoleUtils.error ex.Message   
                2
