﻿namespace RefDataCrawler

open System
open Newtonsoft.Json

type EntityWriterActor(log: PostMessage, crawlStatus: PostMessage, config: CrawlerConfig)=
    
    let rootPath =  if Io.isRooted config.targetPath then
                        config.targetPath
                    else
                        let execPath = System.Reflection.Assembly.GetEntryAssembly().Location |> Io.folder
                        Io.path execPath config.targetPath

                        
    let _ = Io.createFolder rootPath |> Async.RunSynchronously
    
    let postCompleted (entityType, id) =
        ActorMessage.FinishedEntity (entityType, id) |> crawlStatus

    
    let writeEntity (entityType, id, etag, json) =
        let rec writeEntityRecurse() = 
            async {
                let entityId = (entityType,id)
                try
                    let! folder = rootPath |> EsiFiles.entityFolder entityType |> Io.createFolder 
                
                    let dataFilePath = entityId |> EsiFiles.dataFileName |> Io.path folder
                    let metaFilePath = entityId |> EsiFiles.metaFileName |> Io.path folder
                            
                    do! Io.writeString dataFilePath json
            
                    let meta = { EntityMetadata.id = id;
                                                entityType = entityType;
                                                etag = etag} |> JsonConvert.SerializeObject
                        
                    do! Io.writeString metaFilePath meta
            
                    entityId |> postCompleted 

                    sprintf "Entity %s %s written" entityType id |> ActorMessage.Info |> log

                
                with e ->   e.Message |> sprintf "ERROR in %s [%s %s]: %s" typedefof<EntityWriterActor>.Name entityType id |> ActorMessage.Error 
                                |> ( log <--> crawlStatus)
                            do! Async.Sleep(1000)
                            return! writeEntityRecurse()
            }
        writeEntityRecurse() 

    let getEntityMetadata (entityType, id) = 
        async {
            try 
                let folder = rootPath |> EsiFiles.entityFolder entityType
                let filePath = EsiFiles.metaFileName (entityType, id) |> Io.path folder
                let! json = Io.readJson filePath

                return match json with
                        | Some json -> 
                            let meta = JsonConvert.DeserializeObject<EntityMetadata>(json)
                            Some meta
                        | _ -> None
            with _ -> return None
        }

    let pipe = MessageInbox.Start(fun inbox -> 
        
        let rec getNext() = async {
                
            let! inMsg = inbox.Receive()

            match inMsg with
            | Entity (t, id, etag, json) ->             return! (writeEntity (t, id, etag, json))
            | EntityMetadata (entityType, ids, ch) ->   let! results = ids  |> Array.map (fun id -> (entityType, id) |> getEntityMetadata ) 
                                                                            |> Async.Parallel
                                                        results |> ch.Reply
            | _ ->                                      0 |> ignore

            return! getNext()
        }
        
        getNext()
    )

    do pipe.Error.Add(Actors.postException typeof<EntityWriterActor>.Name log)

    member __.Post(msg: ActorMessage) = pipe.Post msg

    member __.GetMetadata(entityType, ids) = 
        pipe.PostAndAsyncReply (fun ch -> ActorMessage.EntityMetadata (entityType, ids, ch))