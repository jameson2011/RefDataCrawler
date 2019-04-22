namespace RefDataCrawler

open System
open DotNetStaticData

type SourceCodeGenerator(config: GenerateConfig)=

    let namespaceName = "StaticData"

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

    let generateUniverseTypes folder =
        async {
            let recordTypeSources = [ typedefof<PositionData>;
                                        typedefof<RegionData>; typedefof<ConstellationData>; typedefof<SolarSystemData>;
                                        typedefof<PlanetData>; typedefof<StarData>; typedefof<StargateData>;
                                        typedefof<AsteroidBeltData>; typedefof<MoonData>; typedefof<StationData>]
                                        |> Seq.map SourceCodeGeneration.toFSharpRecordSource 
                                        |> SourceCodeGeneration.toFSharpTypeDefs namespaceName
                                        |> String.concatenate Environment.NewLine
                                        
            let! recordTypesPath = SourceCodeGeneration.writeFSharpSource folder "Entities" recordTypeSources
            
            return recordTypesPath   
        }

    let generateRegionsSource folder (values: seq<RegionData>)=
        async {
            let id (r: RegionData) = r.id
            let funcName = "getRegion"
            let modulePrefix = "Regions"
            let mapModule = modulePrefix

            return! values |> SourceCodeGeneration.generateEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule
        }               

    
    let generateConstellationsSource folder (values: seq<ConstellationData>)=
        async {
            let id (r: ConstellationData) = r.id
            let funcName = "getConstellation"
            let modulePrefix = "Constellations"
            let mapModule = modulePrefix

            return! values |> SourceCodeGeneration.generateEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule
        }               

    let generateSolarSystemSource folder (values: seq<SolarSystemData>)=
        async {
            let id (r: SolarSystemData) = r.id
            let funcName = "getSolarSystem"
            let modulePrefix = "SolarSystems"
            let mapModule = modulePrefix

            return! values |> SourceCodeGeneration.generateEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule
        }               

    let generatePlanetSource folder (values: seq<PlanetData>)=
        async {
            let id (r: PlanetData) = r.id
            let funcName = "getPlanet"
            let modulePrefix = "Planets"
            let mapModule = modulePrefix

            return! values |> SourceCodeGeneration.generateEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule
        }               

    
    let generateAsteroidBeltSource folder (values: seq<AsteroidBeltData>)=
        async {
            let id (r: AsteroidBeltData) = r.id
            let funcName = "getAsteroidBelt"
            let modulePrefix = "AsteroidBelts"
            let mapModule = modulePrefix

            return! values |> SourceCodeGeneration.generateEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule
        }               

    let generateStarSource folder (values: seq<StarData>)=
        async {
            let id (r: StarData) = r.id
            let funcName = "getStar"
            let modulePrefix = "Stars"
            let mapModule = modulePrefix

            return! values |> SourceCodeGeneration.generateEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule
        }               

    
    let generateStargateSource folder (values: seq<StargateData>)=
        async {
            let id (r: StargateData) = r.id
            let funcName = "getStargate"
            let modulePrefix = "Stargates"
            let mapModule = modulePrefix

            return! values |> SourceCodeGeneration.generateEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule
        }               

    
    let generateStationSource folder (values: seq<StationData>)=
        async {
            let id (r: StationData) = r.id
            let funcName = "getStation"
            let modulePrefix = "Stations"
            let mapModule = modulePrefix

            return! values |> SourceCodeGeneration.generateEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule
        }               

    
    let generateMoonSource folder (values: seq<MoonData>)=
        async {
            let id (r: MoonData) = r.id
            let funcName = "getMoon"
            let modulePrefix = "Moons"
            let mapModule = modulePrefix

            return! values |> SourceCodeGeneration.generateEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule
        }               

    let generateUniverse() =
        async {
            let! rootFolder = "Universe" |> Io.path destinationPath |> Io.createFolder

            let! entitiesModuleFilePath = generateUniverseTypes rootFolder

            
            // TODO:
            let! moons = EsiFiles.moons sourcePath |> Async.map (Seq.map toMoon >> List.ofSeq)
            let! moonFilePaths = moons |> (generateMoonSource rootFolder)
            
            let! regions = EsiFiles.regions sourcePath |> Async.map (Seq.map toRegion >> Array.ofSeq)
            let! regionModuleFilePaths = regions |> generateRegionsSource rootFolder

            let! constellations = EsiFiles.constellations sourcePath |> Async.map (Seq.map toConstellation >> Array.ofSeq)
            let! constellationModuleFilePaths = constellations |> generateConstellationsSource rootFolder

            let! solarSystems = EsiFiles.solarSystems sourcePath  |> Async.map (Seq.map toSolarSystem >> Array.ofSeq)
            let! solarSystemsModuleFilePaths = solarSystems |> generateSolarSystemSource rootFolder
            
            let! planets = EsiFiles.planets sourcePath |> Async.map (Seq.map toPlanet >> Array.ofSeq )
            let! planetsFilePaths = planets |> generatePlanetSource rootFolder

            let! stargates = EsiFiles.stargates sourcePath |> Async.map (Seq.map toStargate >> Array.ofSeq)
            let! stargatesFilePaths = stargates |> generateStargateSource rootFolder

            let! belts = EsiFiles.belts sourcePath |> Async.map (Seq.map toBelt >> Array.ofSeq)
            let! beltsFilePaths = belts |> generateAsteroidBeltSource rootFolder

            let! stars = EsiFiles.stars sourcePath |> Async.map (Seq.map toStar >> Array.ofSeq)
            let! starsFilePaths = stars |> generateStarSource rootFolder

            
            let! stations = EsiFiles.stations sourcePath |> Async.map (Seq.map toStation >> Array.ofSeq)
            let! stationsFilePaths = stations |> generateStationSource rootFolder
            
            // TODO: adjust .fsproj files...

            return true
        }

    let startGeneration() =
        
        ConsoleUtils.info "Starting..."
        
        generateUniverse() |> Async.RunSynchronously |> ignore
        
        ConsoleUtils.info "Done."

        true

    member this.Start() = startGeneration()