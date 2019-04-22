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

    let generateUniverse() =
        async {
            let! rootFolder = "Universe" |> Io.path destinationPath |> Io.createFolder

            let! entitiesSourcePath = generateUniverseTypes rootFolder
            
            let! regions = EsiFiles.regions sourcePath |> Async.map (Seq.map toRegion >> Array.ofSeq)
            
            let rs2 = regions |> SourceCodeGeneration.partitionEntitiesBy config.sourcePartitions (fun r -> r.id)
                              |> Seq.sortBy (fun (p,_) -> p)
                              |> Array.ofSeq

            let rs = regions |> Array.map SourceCodeGeneration.toFSharpRecordInstanceSource
            // TODO: need a way to index / get these... and partition files !


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