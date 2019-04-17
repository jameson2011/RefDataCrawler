namespace RefDataCrawler

type LogPublishActor(configFile: string)= 
        
    do log4net.Config.XmlConfigurator.Configure(System.IO.FileInfo(configFile)) |> ignore


        
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
                | _ ->                      ignore 0
            with e -> onException e
            return! getNext()            
            }
        
        getNext()
    )

    do pipe.Error.Add(onException)

    new() = LogPublishActor("log4net.config")
    
    member __.Post(msg: ActorMessage) = pipe.Post msg

