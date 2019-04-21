namespace RefDataCrawler

type SourceCodeGenerator(config: GenerateConfig)=

    let sourcePath = config.sourcePath
    

    let startGeneration() =
        
        ConsoleUtils.info "Starting..."

        // TODO: 
        
        ConsoleUtils.info "Done."

        true

    member this.Start() = startGeneration()