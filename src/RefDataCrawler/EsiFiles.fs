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
        
    

    let entityFolder rootPath entityType = entityType |> Io.path rootPath

    let dataFileName (entityType, id) = sprintf "%s.%s.%s" entityType id dataFileExtension

    let metaFileName (entityType, id) = sprintf "%s.%s.%s" entityType id metaFileExtension

    let entityFilePaths root entityType =
        entityType  |> entityFolder root 
                    |> dataFiles
        

    let entityFilePath root entityType id =
        let folder = entityType |> entityFolder root
        let filename = (entityType, id) |> dataFileName
        let path = filename |> Io.path folder

        match Io.fileExists path with
        | true ->   Some path
        | _ ->      None
    
    
    let private allEntities root entityType mapper =
        async {
            let tasks = entityType  |> entityFilePaths root 
                                    |> Seq.map Io.readJson
        
            let! jsons = tasks |> Async.Parallel 

            return jsons |> Seq.reduceOptions |> Seq.map mapper |> List.ofSeq

        }

    let regions root = allEntities root "region" Region.Parse

    let constellations root = allEntities root "constellation" Constellation.Parse

    let solarSystems root = allEntities root "system" SolarSystem.Parse

    let stars root = allEntities root "star" Star.Parse

    let planets root = allEntities root "planet" Planet.Parse

    let belts root = allEntities root "belt" AsteroidBelt.Parse

    let moons root = allEntities root "moon" Moon.Parse

    let stations root = allEntities root "station" Station.Parse
