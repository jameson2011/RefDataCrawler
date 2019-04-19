namespace RefDataCrawler

open System
open Newtonsoft.Json

type EntityWriterActor(log: PostMessage, crawlStatus: PostMessage, config: CrawlerConfig)=
    
    let rootPath =  if Io.isRooted config.targetPath then
                        config.targetPath
                    else
                        let execPath = System.Reflection.Assembly.GetEntryAssembly().Location |> Io.folder
                        Io.path execPath config.targetPath

                        
    do Io.createFolder rootPath |> Async.RunSynchronously
    
    let createFolder path = 
        async {
            do! path |> Io.createFolder 
            return path
        }
        
    let postCompleted (entityType, id) =
        ActorMessage.FinishedEntity (entityType, id) |> crawlStatus

    let entityFolder entityType = entityType |> Io.path rootPath

    let dataFileName (entityType, id) = sprintf "%s.%s.data.json" entityType id 

    let metaFileName (entityType, id) = sprintf "%s.%s.meta.json" entityType id 

    let writeEntity (entityType, id, etag, json) =
        let rec writeEntityRecurse() = 
            async {
                let entityId = (entityType,id)
                try
                    let! folder = entityType |> entityFolder |> createFolder 
                
                    let dataFilePath = entityId |> dataFileName |> Io.path folder
                    let metaFilePath = entityId |> metaFileName |> Io.path folder
                            
                    do! Io.writeJson dataFilePath json
            
                    let meta = { EntityMetadata.id = id;
                                                entityType = entityType;
                                                captured = DateTimeOffset.UtcNow;
                                                etag = etag} |> JsonConvert.SerializeObject
                        
                    do! Io.writeJson metaFilePath meta
            
                    entityId |> postCompleted 

                    sprintf "Entity %s %s written" entityType id |> ActorMessage.Info |> log

                
                with e ->   e.Message |> sprintf "ERROR in %s [%s %s]: %s" typedefof<EntityWriterActor>.Name entityType id |> ActorMessage.Error 
                                |> ( log <--> crawlStatus)
                            do! Async.Sleep(1000)
                            return! writeEntityRecurse()
            }
        writeEntityRecurse() 

    let pipe = MessageInbox.Start(fun inbox -> 
        
        let rec getNext() = async {
                
            let! inMsg = inbox.Receive()

            match inMsg with
            | Entity (t, id, etag, json) -> return! (writeEntity (t, id, etag, json))
            | _ -> 0 |> ignore

            return! getNext()
        }
        
        getNext()
    )

    do pipe.Error.Add(Actors.postException typeof<CrawlerActor>.Name log)

    member __.Post(msg: ActorMessage) = pipe.Post msg

