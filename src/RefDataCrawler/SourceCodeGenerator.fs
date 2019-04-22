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

    let generateRegions folder =
        async {
            let! regions = EsiFiles.regions sourcePath |> Async.map (Seq.map toRegion >> Array.ofSeq)
            
            let id (r: RegionData) = r.id
            let funcName = "getRegion"
            let modulePrefix = "Regions"
            let mapModule = modulePrefix


            // Below can be factored out
            let moduleName prefix b = (sprintf "%s%i" prefix b)
            
            let moduleFuncs = 
                    regions   |> SourceCodeGeneration.partitionEntitiesById config.sourcePartitions id
                              |> Seq.groupBy fst
                              |> Seq.map (fun (bkt,xs) ->   (bkt, xs |> Seq.map snd |> Array.ofSeq))
                              |> Seq.map (fun (bkt,xs) ->   let funcSource = xs |> SourceCodeGeneration.toFSharpGenEntityFunction funcName id SourceCodeGeneration.toFSharpRecordInstanceSource 
                                                            (bkt, (moduleName modulePrefix bkt), funcName, funcSource))
                              |> Array.ofSeq
                              
            
            let modules = 
                moduleFuncs 
                                |> Seq.map (fun (bkt, modName, funcName, source) -> let moduleSource = SourceCodeGeneration.toFSharpModule namespaceName modName source  
                                                                                                            |> String.concatenate Environment.NewLine
                                                                                    let moduleFilePath = SourceCodeGeneration.writeFSharpSource folder modName moduleSource 
                                                                                                            |> Async.RunSynchronously 
                                                                                    (bkt, modName, funcName, moduleFilePath) )
                                |> Array.ofSeq

            let moduleFilePaths = modules |> Seq.map (fun (_,_,_,path) -> path) |> List.ofSeq

            // generate a module that indexes all modules/functions
            let mapModuleFilePath = 
                            modules |> Seq.map (fun (bkt, modName, funcName, _) -> (bkt, modName, funcName))
                                    |> SourceCodeGeneration.toFSharpGenMapFunction funcName config.sourcePartitions 
                                    |> SourceCodeGeneration.toFSharpModule namespaceName mapModule 
                                    |> String.concatenate Environment.NewLine
                                    |> SourceCodeGeneration.writeFSharpSource folder mapModule
                                    |> Async.RunSynchronously
                                    
            // return module files in correct project order
            let result = moduleFilePaths @ [ mapModuleFilePath ]
            return result
            
        }               

    let generateUniverse() =
        async {
            let! rootFolder = "Universe" |> Io.path destinationPath |> Io.createFolder

            let! entitiesModuleFilePath = generateUniverseTypes rootFolder
            
            let! regionModuleFilePaths = generateRegions rootFolder

            let! constellations = EsiFiles.constellations sourcePath |> Async.map (Seq.map toConstellation >> Array.ofSeq)
            let cs = constellations |> Array.map SourceCodeGeneration.toFSharpRecordInstanceSource

            let! solarSystems = EsiFiles.solarSystems sourcePath  |> Async.map (Seq.map toSolarSystem >> Array.ofSeq)
            let ss = solarSystems |> Array.map SourceCodeGeneration.toFSharpRecordInstanceSource

            let! planets = EsiFiles.planets sourcePath |> Async.map (Seq.map toPlanet >> Seq.length )

            let! moons = EsiFiles.moons sourcePath |> Async.map (Seq.map toMoon >> Seq.length)
            let! stargates = EsiFiles.stargates sourcePath |> Async.map (Seq.map toStargate >> Seq.length)
            
            let! belts = EsiFiles.belts sourcePath |> Async.map (Seq.map toBelt >> Array.ofSeq)
            let! stars = EsiFiles.stars sourcePath |> Async.map (Seq.map toStar >> Array.ofSeq)
        
            return true
        }

    let startGeneration() =
        
        ConsoleUtils.info "Starting..."
        
        generateUniverse() |> Async.RunSynchronously |> ignore
        
        ConsoleUtils.info "Done."

        true

    member this.Start() = startGeneration()