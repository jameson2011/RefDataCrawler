namespace RefDataCrawler

type LogPublishActor(config: CrawlerConfig, configFile: string)= 
        
    let log4netRepo = log4net.LogManager.GetRepository(System.Reflection.Assembly.GetEntryAssembly())
    let configFileInfo = System.IO.FileInfo(configFile)
    do log4net.Config.XmlConfigurator.ConfigureAndWatch(log4netRepo, configFileInfo) |> ignore
    
    
    let repo = log4netRepo :?> log4net.Repository.Hierarchy.Hierarchy
    do 
        if (not config.verboseLogging) && repo <> null then
            do repo.Root.RemoveAppender("Console") |> ignore
      
    let logger = log4net.LogManager.GetLogger(typeof<LogPublishActor>)

    
    let logInfo (msg: string) = logger.Info(msg)
            
    let logTrace (msg: string) = logger.Debug(msg)

    let logWarn (msg: string) = logger.Warn(msg)

    let logError (msg: string) = logger.Error(msg)

    let logException (source: string) (ex: System.Exception) = logger.Error(source, ex)

    let onException = logException typeof<LogPublishActor>.Name 

    let pipe = MessageInbox.Start(fun inbox -> 
        let rec getNext() = async {
            let! msg = inbox.Receive()

            try
                match msg with
                | Warning msg ->            msg |> logWarn
                | Error msg ->              msg |> logError
                | Exception (source, ex) -> logException source ex               
                | Info msg ->               msg |> logInfo
                | Trace msg ->              msg |> logTrace
                | Ping ch ->                ignore 0 |> ch.Reply 
                | _ ->                      ignore 0
            with e -> onException e
            return! getNext()            
            }
        
        getNext()
    )

    do pipe.Error.Add(onException)

    new(config: CrawlerConfig) = LogPublishActor(config, "log4net.config")
    
    member __.Post(msg: ActorMessage) = pipe.Post msg

    member __.Ping() = 
        pipe.PostAndAsyncReply (fun ch -> ActorMessage.Ping ch)
        

