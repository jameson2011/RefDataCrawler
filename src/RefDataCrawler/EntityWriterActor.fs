namespace RefDataCrawler

open System
open Newtonsoft.Json

type EntityWriterActor(log: PostMessage, crawlStatus: PostMessage, config: CrawlerConfig)=
    
    let rootPath =  if Io.isRooted config.targetPath then
                        config.targetPath
                    else
                        let execPath = System.Reflection.Assembly.GetEntryAssembly().Location |> Io.folder
                        Io.path execPath config.targetPath


    // TODO: delete old folder if necessary...
    do Io.createFolder rootPath |> Async.RunSynchronously
    
    let createFolder path = 
        async {
            do! path |> Io.createFolder 
            return path
        }

    let path = Io.path rootPath

    let postDiscovered entityType ids =
        ids 
            |> Seq.map string
            |> Seq.map (fun s -> (entityType, s)) |> Seq.map ActorMessage.FinishedEntity |> Seq.iter crawlStatus


    let write (t, id, etag, json) =
        async {
            let! folder = t |> path |> createFolder 

            
            let meta = { EntityMetadata.id = id;
                                        entityType = t;
                                        captured = DateTimeOffset.UtcNow;
                                        etag = etag} |> JsonConvert.SerializeObject
            
            let metaFilePath = sprintf "%s.%s.meta.json" t id |> Io.path folder
            do! Io.writeJson metaFilePath meta
           
            let filePath = sprintf "%s.%s.data.json" t id |> Io.path folder

            do! Io.writeJson filePath json
            
            [ id ] |> postDiscovered t

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

