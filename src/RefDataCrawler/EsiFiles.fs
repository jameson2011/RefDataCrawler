namespace RefDataCrawler

module EsiFiles=
    
    [<Literal>]
    let private dataFileExtension = "data.json"

    [<Literal>]
    let private metaFileExtension = "meta.json"
    
    let private entityId (path: string) =
        let filename = System.IO.Path.GetFileName(path)
        let segs = filename.Split('.')
        (segs.[0], segs.[1])

    
    let private dataFiles folder =
        System.IO.Directory.EnumerateFiles(folder, "*." + dataFileExtension)
        
    let private readEntityJson path = 
        async {
            let id = entityId path   
            let! json = Io.readJson path
            return match json with
                    | Some j -> Some (snd id, j)
                    | _ ->      None
        }

    let entityFolder entityType rootPath = entityType |> Io.path rootPath

    let dataFileName (entityType, id) = sprintf "%s.%s.%s" entityType id dataFileExtension

    let metaFileName (entityType, id) = sprintf "%s.%s.%s" entityType id metaFileExtension

    let entityFilePaths root entityType =
        root  |> entityFolder entityType 
              |> dataFiles
        

    let entityFilePath root entityType id =
        let folder = root |> entityFolder entityType
        let filename = (entityType, id) |> dataFileName
        let path = filename |> Io.path folder

        match Io.fileExists path with
        | true ->   Some path
        | _ ->      None
    
    
    let private allEntities root entityType mapper =
        async {
            let map (id: string, json) = (id, mapper json)
            let result = entityType |> entityFilePaths root 
                                    |> Seq.map (readEntityJson >> Async.RunSynchronously)
                                    |> Seq.reduceOptions
                                    |> Seq.map map
            
            return result
        }

    let regions root = allEntities root "region" Region.Parse

    let constellations root = allEntities root "constellation" Constellation.Parse

    let solarSystems root = allEntities root "system" SolarSystem.Parse

    let stars root = allEntities root "star" Star.Parse

    let planets root = allEntities root "planet" Planet.Parse

    let belts root = allEntities root "belt" AsteroidBelt.Parse

    let moons root = allEntities root "moon" Moon.Parse

    let stations root = allEntities root "station" (fun j -> Station.Parse j)

    let stargates root = allEntities root "stargate" Stargate.Parse

    let marketGroups root = allEntities root "market_group" MarketGroup.Parse

    let categories root = allEntities root "category" Category.Parse

    let groups root = allEntities root "group" Group.Parse

    let dogmaAttributes root = allEntities root "dogma_attribute" DogmaAttribute.Parse

    let dogmaEffect root = allEntities root "dogma_effect" DogmaEffect.Parse

    let itemTypes root = allEntities root "type" ItemType.Parse

    let npcCorps root = allEntities root "npccorps" NpcCorp.Parse