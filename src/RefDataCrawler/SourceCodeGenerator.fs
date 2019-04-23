namespace RefDataCrawler

open System
open DotNetStaticData

type SourceCodeGenerator(config: GenerateConfig)=

    let namespacePrefix = config.namespacePrefix
    let sourcePath = config.sourcePath
    let destinationPath = config.targetPath
    
    let toRegion (id: string, value: Region.Root) = 
        { RegionData.id = value.RegionId; 
                    name = value.Name; 
                    constellationIds = value.Constellations; 
                    }

    let toConstellation (id: string, value: Constellation.Root) =
        { ConstellationData.id = value.ConstellationId;
                            name = value.Name;
                            regionId = value.RegionId;
                            solarSystemIds = value.Systems;
                            position = { PositionData.x = float value.Position.X; 
                                                      y = float value.Position.Y; 
                                                      z = float value.Position.Z}
                            }

    let toSolarSystem (id: string, value: SolarSystem.Root) =
        let planets = safeDefault (fun () -> value.Planets) [||]
        let belts = planets |> Array.collect (fun p -> safeDefault (fun () -> p.AsteroidBelts) [||] )
        let moons = planets |> Array.collect (fun p -> safeDefault (fun () -> p.Moons) [||] )

        { SolarSystemData.id = value.SystemId;
                        name = value.Name;
                        constellationId = value.ConstellationId;
                        position = { PositionData.x = float value.Position.X; 
                                                    y = float value.Position.Y; 
                                                    z = float value.Position.Z};
                        secStatus = float value.SecurityStatus;
                        starIds = safeDefault (fun () -> [| value.StarId |] ) [||] ;
                        planetIds = planets |> Array.map (fun p -> p.PlanetId);
                        beltIds = belts;
                        moonIds = moons;
                        stargateIds = safeDefault (fun () -> value.Stargates) [||];
                        stationIds = safeDefault (fun () -> value.Stations) [||];
                        }
        
    let toPlanet (id: string, value: Planet.Root) =
        {
            PlanetData.id = value.PlanetId;
                       name = value.Name;
                       solarSystemId = value.SystemId;
                       position = { PositionData.x = float value.Position.X; 
                                                    y = float value.Position.Y; 
                                                    z = float value.Position.Z};
                       typeId = value.TypeId;
        }

    let toStar (id: string, value: Star.Root) =
        {
            StarData.id = Int32.Parse id;
                     name = value.Name;
                     solarSystemId = value.SolarSystemId;
                     typeId = value.TypeId;
        }

    let toStargate (id: string, value: Stargate.Root) =
        {
            StargateData.id = value.StargateId;
                        name = value.Name;
                        solarSystemId = value.SystemId;
                        position = { PositionData.x = float value.Position.X; 
                                                    y = float value.Position.Y; 
                                                    z = float value.Position.Z};
                        typeId = value.TypeId;
                        destinationSolarSystemId = value.Destination.SystemId;
                        destinationStargateId = value.Destination.StargateId;
        }

    let toMoon (id: string, value: Moon.Root) =
        {
            MoonData.id = value.MoonId;
                    name = value.Name;
                    solarSystemId = value.SystemId;
                    position = { PositionData.x = float value.Position.X; 
                                                    y = float value.Position.Y; 
                                                    z = float value.Position.Z};
        }

    let toBelt (id: string, value: AsteroidBelt.Root) = 
        {
            AsteroidBeltData.id = Int32.Parse id;
                            name = value.Name;
                            solarSystemId = value.SystemId;
                            position = { PositionData.x = float value.Position.X; 
                                                        y = float value.Position.Y; 
                                                        z = float value.Position.Z};
        }

    let toStation (id: string, value: Station.Root)=
        {
            StationData.id = value.StationId;
                        name = value.Name;
                        solarSystemId = value.SystemId;
                        position = { PositionData.x = float value.Position.X; 
                                                        y = float value.Position.Y; 
                                                        z = float value.Position.Z};
                        typeId = value.TypeId;
                        services = value.Services;
                        maxDockableShipVolume = value.MaxDockableShipVolume;
        }


    let generateTypeDefinitions namespaceName folder (types: seq<Type>) =
        async {
            let recordTypeSources = 
                    types   |> Seq.map FSharpSource.toRecordSource 
                            |> FSharpSource.toTypeDefs namespaceName
                            |> String.concatenate Environment.NewLine
                                        
            let! recordTypesPath = FSharpSource.writeSource folder "Entities" recordTypeSources
            
            return recordTypesPath   
        }

    let generateUniverseTypes namespaceName folder =
        async {
            return! [   typedefof<PositionData>;
                        typedefof<RegionData>; typedefof<ConstellationData>; typedefof<SolarSystemData>;
                        typedefof<PlanetData>; typedefof<StarData>; typedefof<StargateData>;
                        typedefof<AsteroidBeltData>; 
                        typedefof<StationData> ]
                        |> generateTypeDefinitions namespaceName folder
        }

    
    let generateMoonTypes namespaceName folder =
        async {
            return! [ typedefof<MoonData>; ]
                        |> generateTypeDefinitions namespaceName folder
        }

    let generateRegionsSource namespaceName folder (values: RegionData[])=
        async {
            let id (r: RegionData) = r.id
            let funcName = "getRegion"
            let modulePrefix = "Regions"
            let mapModule = modulePrefix
            
            let enumeratorFunction = values |> Seq.map id 
                                        |> FSharpSource.toEntityEnumerator "regions" funcName 
                                        |> List.ofSeq
            

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule enumeratorFunction
        }               

    
    let generateConstellationsSource namespaceName folder (values: seq<ConstellationData>)=
        async {
            let id (r: ConstellationData) = r.id
            let funcName = "getConstellation"
            let modulePrefix = "Constellations"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule []
        }               

    let generateSolarSystemSource namespaceName folder (values: seq<SolarSystemData>)=
        async {
            let id (r: SolarSystemData) = r.id
            let funcName = "getSolarSystem"
            let modulePrefix = "SolarSystems"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule []
        }               

    let generatePlanetSource namespaceName folder (values: seq<PlanetData>)=
        async {
            let id (r: PlanetData) = r.id
            let funcName = "getPlanet"
            let modulePrefix = "Planets"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule []
        }               

    
    let generateAsteroidBeltSource namespaceName folder (values: seq<AsteroidBeltData>)=
        async {
            let id (r: AsteroidBeltData) = r.id
            let funcName = "getAsteroidBelt"
            let modulePrefix = "AsteroidBelts"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule []
        }               

    let generateStarSource namespaceName folder (values: seq<StarData>)=
        async {
            let id (r: StarData) = r.id
            let funcName = "getStar"
            let modulePrefix = "Stars"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule []
        }               

    
    let generateStargateSource namespaceName folder (values: seq<StargateData>)=
        async {
            let id (r: StargateData) = r.id
            let funcName = "getStargate"
            let modulePrefix = "Stargates"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule []
        }               

    
    let generateStationSource namespaceName folder (values: seq<StationData>)=
        async {
            let id (r: StationData) = r.id
            let funcName = "getStation"
            let modulePrefix = "Stations"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule []
        }               

    
    let generateMoonSource namespaceName folder (values: seq<MoonData>)=
        async {
            let id (r: MoonData) = r.id
            let funcName = "getMoon"
            let modulePrefix = "Moons"
            let mapModule = modulePrefix

            let partitions = Math.primeBefore 300

            return! values |> FSharpSource.genEntitiesSource folder partitions namespaceName id funcName modulePrefix mapModule []
        }               

    let generateMoons(domain: string) =
        async {
            let namespaceName = FSharpSource.namespaceName namespacePrefix domain
            let projectFileName = FSharpSource.projectFileName namespaceName

            let! rootFolder = namespaceName |> Io.path destinationPath |> Io.createFolder

            let! entitiesModuleFilePath = rootFolder |> generateMoonTypes namespaceName

            
            let! moons = EsiFiles.moons sourcePath |> Async.map (Seq.map toMoon >> Array.ofSeq)
            let! moonMapFile, moonDataFiles = moons |> generateMoonSource namespaceName rootFolder

            
            let! projectResult = FSharpSource.genProjectFile rootFolder projectFileName  [ entitiesModuleFilePath ] moonDataFiles [ moonMapFile ]

            return true
        }

    let generateUniverse(domain: string) =
        async {
            let namespaceName = FSharpSource.namespaceName namespacePrefix domain
            let projectFileName = FSharpSource.projectFileName namespaceName

            let! rootFolder = namespaceName |> Io.path destinationPath |> Io.createFolder

            let! entitiesModuleFilePath = rootFolder |> generateUniverseTypes namespaceName
            
            let! regions = EsiFiles.regions sourcePath |> Async.map (Seq.map toRegion >> Array.ofSeq)
            let! regionMapFile, regionDataFiles = regions |> generateRegionsSource namespaceName rootFolder

            let! constellations = EsiFiles.constellations sourcePath |> Async.map (Seq.map toConstellation >> Array.ofSeq)
            let! constellationMapFile, constellationDataFiles = constellations |> generateConstellationsSource namespaceName rootFolder

            let! solarSystems = EsiFiles.solarSystems sourcePath  |> Async.map (Seq.map toSolarSystem >> Array.ofSeq)
            let! solarSystemMapFile, solarSystemDataFiles = solarSystems |> generateSolarSystemSource namespaceName rootFolder
            
            let! planets = EsiFiles.planets sourcePath |> Async.map (Seq.map toPlanet >> Array.ofSeq )
            let! planetMapFile, planetDataFiles = planets |> generatePlanetSource namespaceName rootFolder

            let! stargates = EsiFiles.stargates sourcePath |> Async.map (Seq.map toStargate >> Array.ofSeq)
            let! stargateMapFile, stargateDataFiles = stargates |> generateStargateSource namespaceName rootFolder

            let! belts = EsiFiles.belts sourcePath |> Async.map (Seq.map toBelt >> Array.ofSeq)
            let! beltMapFile, beltDataFiles = belts |> generateAsteroidBeltSource namespaceName rootFolder

            let! stars = EsiFiles.stars sourcePath |> Async.map (Seq.map toStar >> Array.ofSeq)
            let! starMapFile, starDataFiles = stars |> generateStarSource namespaceName rootFolder

            
            let! stations = EsiFiles.stations sourcePath |> Async.map (Seq.map toStation >> Array.ofSeq)
            let! stationMapFile, stationDataFiles = stations |> generateStationSource namespaceName rootFolder
            
            // adjust .fsproj files...
            let top = [ entitiesModuleFilePath ]
            let data = [ regionDataFiles; constellationDataFiles; solarSystemDataFiles; planetDataFiles; stargateDataFiles; beltDataFiles; stationDataFiles]
                        |> Seq.collect (fun xs -> xs)
            let maps = [ regionMapFile; constellationMapFile; solarSystemMapFile; planetMapFile; stargateMapFile; beltMapFile; stationMapFile]
                    
            let! projectResult = FSharpSource.genProjectFile rootFolder projectFileName top data maps

            return true
        }

    let startGeneration() =
        
        "Starting..." |> ConsoleUtils.info 
        
        let start = DateTime.UtcNow

        generateUniverse "Universe" |> Async.RunSynchronously |> ignore
        
        generateMoons "Moons" |> Async.RunSynchronously |> ignore

        let duration = DateTime.UtcNow - start
        "Done. " + duration.ToString() |> ConsoleUtils.info

        true

    member this.Start() = startGeneration()