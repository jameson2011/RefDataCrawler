namespace RefDataCrawler


module Program =
    
    let private crawlerConfig (app: CommandLine.App) =
        let verboseLogging = CommandLine.getVerboseValue app;
        let showProgressTicker = CommandLine.getProgressTickerValue app;
        
        let verboseLogging = if showProgressTicker then false
                             else true

        { CrawlerConfig.targetPath = CommandLine.getTargetFolderValue app |> Option.defaultValue CrawlerConfig.TargetPathDefault;
                          crawlRegions = CommandLine.getRegionsValue app;
                          crawlConstellations = CommandLine.getConstellationsValue app;
                          crawlSystems = CommandLine.getSystemsValue app;
                          crawlGroups = CommandLine.getGroupsValue app;
                          crawlCategories = CommandLine.getCategoriesValue app;
                          crawlTypes = CommandLine.getTypesValue app;
                          crawlDogmaAttributes = CommandLine.getDogmaAttributesValue app;
                          crawlDogmaEffects = CommandLine.getDogmaEffectsValue app;
                          crawlMarketGroups = CommandLine.getMarketGroupsValue app;
                          verboseLogging = verboseLogging;
                          showProgressTicker = showProgressTicker;
                          maxErrors = CommandLine.getMaxErrorsValue app;
        }
        
    let private generateConfig (app: CommandLine.App) =
        { GenerateConfig.sourcePath = CommandLine.getSourceFolderArg app |> Option.defaultValue @"C:\\";
                         targetPath = CommandLine.getTargetFolderValue app |> Option.defaultValue GenerateConfig.TargetPathDefault;
                         sourcePartitions = Math.primeBefore 100;
                         namespacePrefix = "EsiStatics";
        }

    let private sdeCrawlerConfig (app: CommandLine.App)=
        { SdeCrawlerConfig.targetPath = CommandLine.getTargetFolderValue app |> Option.defaultValue SdeCrawlerConfig.TargetPathDefault;
            sourcePath = CommandLine.getSourceUriArg app;
            verboseLogging = CommandLine.getVerboseValue app;
        }
        
    let private startCrawler (app: CommandLine.App) =        
        let config = crawlerConfig app

        let crawler = new EsiCrawler(config)

        crawler.Start()

    let private startGenerate (app: CommandLine.App) =
        let config = generateConfig app

        let gen = new SourceCodeGenerator(config)

        gen.Start()

    let private startSdeCrawl (app: CommandLine.App) =
        let config = sdeCrawlerConfig app
        let gen = new SdeCrawler(config)
        gen.Start()

    let private createAppTemplate()=
        let app = CommandLine.createApp()
                    |> CommandLine.addCrawl startCrawler
                    |> CommandLine.addGenerate startGenerate
                    |> CommandLine.addCrawlSde startSdeCrawl
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
                ConsoleUtils.error ex.StackTrace
                2
