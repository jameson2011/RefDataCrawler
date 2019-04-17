﻿namespace RefDataCrawler

open System
open Newtonsoft.Json

type DataWriterActor(log: PostMessage, config: CrawlerConfig)=
    
    
    let execPath = System.Reflection.Assembly.GetEntryAssembly().Location |> Io.folder
    let rootPath = Io.path execPath config.targetPath
    // TODO: delete old folder if necessary...
    do Io.createFolder rootPath |> Async.RunSynchronously
    
    let createFolder path = 
        async {
            do! path |> Io.createFolder 
            return path
        }

    let path = Io.path rootPath


    let write (t, id, etag, json) =
        async {
            let! folder = t |> path |> createFolder 

            
            let meta = { EntityMetadata.id = id;
                                        entityType = t;
                                        captured = DateTimeOffset.UtcNow;
                                        etag = etag} |> JsonConvert.SerializeObject
            
            let metaFilePath = sprintf "%s.%s.meta.json" t id |> Io.path folder
            do! Io.writeJson metaFilePath meta
           
            let filePath = sprintf "%s.%s.json" t id |> Io.path folder

            do! Io.writeJson filePath json
            
            sprintf "Entity %s %s written" t id |> ActorMessage.Info |> log
        }


    let pipe = MessageInbox.Start(fun inbox -> 
        
        let rec getNext() = async {
                
            let! inMsg = inbox.Receive()

            match inMsg with
            | Entity (t, id, etag, json) -> return! (write (t, id, etag, json))
            | _ -> 0 |> ignore

            return! getNext()
        }
        
        getNext()
    )

    do pipe.Error.Add(Actors.postException typeof<CrawlerActor>.Name log)

    member __.Post(msg: ActorMessage) = pipe.Post msg

