namespace RefDataCrawler

open System

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
                        typedefof<AsteroidBeltData>; typedefof<StationData>; typedefof<MoonData>;
                        typedefof<CategoryData>; typedefof<GroupData>; typedefof<MarketGroupData>;
                        typedefof<DogmaAttributeValueData>; typedefof<DogmaEffectValueData>; typedefof<ItemTypeData>;
                        typedefof<DogmaAttributeData>; typedefof<DogmaEffectData>;
                        ]
                        |> generateTypeDefinitions namespaceName folder
        }

    
    let generateRegionsSource namespaceName folder importedNamespaces (values: RegionData[])=
        async {
            let id (r: RegionData) = r.id
            let funcName = "getRegion"
            let modulePrefix = "Regions"
            let mapModule = modulePrefix
            
            let enumeratorFunction = values |> Seq.map id 
                                        |> FSharpSource.toEntityEnumerator "regions" funcName 
                                        |> List.ofSeq
            

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule enumeratorFunction importedNamespaces
        }               

    
    let generateConstellationsSource namespaceName folder importedNamespaces (values: seq<ConstellationData>)=
        async {
            let id (r: ConstellationData) = r.id
            let funcName = "getConstellation"
            let modulePrefix = "Constellations"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule [] importedNamespaces
        }               

    let generateSolarSystemSource namespaceName folder importedNamespaces (values: seq<SolarSystemData>)=
        async {
            let id (r: SolarSystemData) = r.id
            let funcName = "getSolarSystem"
            let modulePrefix = "SolarSystems"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule [] importedNamespaces
        }               

    let generatePlanetSource namespaceName folder importedNamespaces (values: seq<PlanetData>)=
        async {
            let id (r: PlanetData) = r.id
            let funcName = "getPlanet"
            let modulePrefix = "Planets"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule [] importedNamespaces
        }               

    
    let generateAsteroidBeltSource namespaceName folder importedNamespaces  (values: seq<AsteroidBeltData>)=
        async {
            let id (r: AsteroidBeltData) = r.id
            let funcName = "getAsteroidBelt"
            let modulePrefix = "AsteroidBelts"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule [] importedNamespaces
        }               

    let generateStarSource namespaceName folder importedNamespaces (values: seq<StarData>)=
        async {
            let id (r: StarData) = r.id
            let funcName = "getStar"
            let modulePrefix = "Stars"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule [] importedNamespaces
        }               

    
    let generateStargateSource namespaceName folder importedNamespaces (values: seq<StargateData>)=
        async {
            let id (r: StargateData) = r.id
            let funcName = "getStargate"
            let modulePrefix = "Stargates"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule [] importedNamespaces
        }               

    
    let generateStationSource namespaceName folder importedNamespaces (values: seq<StationData>)=
        async {
            let id (r: StationData) = r.id
            let funcName = "getStation"
            let modulePrefix = "Stations"
            let mapModule = modulePrefix

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule [] importedNamespaces
        }               

    
    let generateMoonSource namespaceName folder importedNamespaces (values: seq<MoonData>)=
        async {
            let id (r: MoonData) = r.id
            let funcName = "getMoon"
            let modulePrefix = "Moons"
            let mapModule = modulePrefix

            let partitions = Math.primeBefore 300

            return! values |> FSharpSource.genEntitiesSource folder partitions namespaceName id funcName modulePrefix mapModule [] importedNamespaces
        }               

    let generateSharedTypes(domain: string)=
        async {
            let namespaceName = FSharpSource.namespaceName namespacePrefix domain
            let projectFileName = FSharpSource.projectFileName namespaceName

            let! rootFolder = namespaceName |> Io.path destinationPath |> Io.createFolder
            
            let! entitiesModuleFilePath = rootFolder |> generateUniverseTypes namespaceName
            
            let! projectFilePath = FSharpSource.genProjectFile rootFolder projectFileName  [ entitiesModuleFilePath ] [] [] []
            
            return (namespaceName, projectFilePath)
        }


    let generateMoons(domain: string) (importedNamespaces: seq<string>) (sharedTypesPath: string)=
        async {
            let namespaceName = FSharpSource.namespaceName namespacePrefix domain
            let projectFileName = FSharpSource.projectFileName namespaceName

            let! rootFolder = namespaceName |> Io.path destinationPath |> Io.createFolder
            
            
            let! moons = EsiFiles.moons sourcePath |> Async.map (Seq.map toMoon >> Array.ofSeq)
            let! moonMapFile, moonDataFiles = moons |> generateMoonSource namespaceName rootFolder importedNamespaces

            
            let! projectFilePath = FSharpSource.genProjectFile rootFolder projectFileName  [] moonDataFiles [moonMapFile] [sharedTypesPath]

            return (namespaceName, projectFilePath)
        }

    let generateUniverse(domain: string) (sharedTypesNamespace: seq<string>) (sharedTypesPath: string)=
        async {
            let namespaceName = FSharpSource.namespaceName namespacePrefix domain
            let projectFileName = FSharpSource.projectFileName namespaceName

            let! rootFolder = namespaceName |> Io.path destinationPath |> Io.createFolder

            // TODO: this lot is fugly...
            let! regions = EsiFiles.regions sourcePath |> Async.map (Seq.map toRegion >> Array.ofSeq)
            let! regionMapFile, regionDataFiles = regions |> generateRegionsSource namespaceName rootFolder sharedTypesNamespace

            let! constellations = EsiFiles.constellations sourcePath |> Async.map (Seq.map toConstellation >> Array.ofSeq)
            let! constellationMapFile, constellationDataFiles = constellations |> generateConstellationsSource namespaceName rootFolder sharedTypesNamespace

            let! solarSystems = EsiFiles.solarSystems sourcePath  |> Async.map (Seq.map toSolarSystem >> Array.ofSeq)
            let! solarSystemMapFile, solarSystemDataFiles = solarSystems |> generateSolarSystemSource namespaceName rootFolder sharedTypesNamespace
            
            let! planets = EsiFiles.planets sourcePath |> Async.map (Seq.map toPlanet >> Array.ofSeq )
            let! planetMapFile, planetDataFiles = planets |> generatePlanetSource namespaceName rootFolder sharedTypesNamespace

            let! stargates = EsiFiles.stargates sourcePath |> Async.map (Seq.map toStargate >> Array.ofSeq)
            let! stargateMapFile, stargateDataFiles = stargates |> generateStargateSource namespaceName rootFolder sharedTypesNamespace

            let! belts = EsiFiles.belts sourcePath |> Async.map (Seq.map toBelt >> Array.ofSeq)
            let! beltMapFile, beltDataFiles = belts |> generateAsteroidBeltSource namespaceName rootFolder sharedTypesNamespace

            let! stars = EsiFiles.stars sourcePath |> Async.map (Seq.map toStar >> Array.ofSeq)
            let! starMapFile, starDataFiles = stars |> generateStarSource namespaceName rootFolder sharedTypesNamespace

            
            let! stations = EsiFiles.stations sourcePath |> Async.map (Seq.map toStation >> Array.ofSeq)
            let! stationMapFile, stationDataFiles = stations |> generateStationSource namespaceName rootFolder sharedTypesNamespace
            
            // adjust .fsproj...
            let data = [ regionDataFiles; constellationDataFiles; solarSystemDataFiles; planetDataFiles; stargateDataFiles; beltDataFiles; stationDataFiles; starDataFiles; ]
                        |> Seq.collect (fun xs -> xs)
            let maps = [ regionMapFile; constellationMapFile; solarSystemMapFile; planetMapFile; stargateMapFile; beltMapFile; stationMapFile; starMapFile]
                    
            
            let! projectFilePath = FSharpSource.genProjectFile rootFolder projectFileName [] data maps [sharedTypesPath]

            return (namespaceName, projectFilePath)
        }

    let startGeneration() =
        
        "Starting..." |> ConsoleUtils.info 
        
        let start = DateTime.UtcNow

        let sharedTypesNamespace, sharedTypesProject = generateSharedTypes "Entities" |> Async.RunSynchronously
        
        generateUniverse "Universe" [sharedTypesNamespace] sharedTypesProject |> Async.RunSynchronously |> ignore
        
        generateMoons "Moons" [sharedTypesNamespace] sharedTypesProject |> Async.RunSynchronously |> ignore

        let duration = DateTime.UtcNow - start
        "Done. " + duration.ToString() |> ConsoleUtils.info

        true

    member this.Start() = startGeneration()