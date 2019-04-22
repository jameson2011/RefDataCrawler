namespace RefDataCrawler

open DotNetStaticData

type SourceCodeGenerator(config: GenerateConfig)=

    let sourcePath = config.sourcePath
    
    let toRegion(value: Region.Root) = 
        
        { RegionData.id = value.RegionId; 
                    name = value.Name; 
                    constellationIds = value.Constellations; 
                    }

    let toConstellation(value: Constellation.Root) =
        { ConstellationData.id = value.ConstellationId;
                            name = value.Name;
                            regionId = value.RegionId;
                            solarSystemIds = value.Systems;
                            position = { PositionData.x = float value.Position.X; 
                                                      y = float value.Position.Y; 
                                                      z = float value.Position.Z}
                            }

    let toSolarSystem(value: SolarSystem.Root) =
        
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
        

    let toPlanet(value: Planet.Root) =
        {
            PlanetData.id = value.PlanetId;
                       name = value.Name;
                       solarSystemId = value.SystemId;
                       position = { PositionData.x = float value.Position.X; 
                                                    y = float value.Position.Y; 
                                                    z = float value.Position.Z};
                       typeId = value.TypeId;
        }

    let toStar (value: Star.Root) =
        {
            StarData.id = value.SolarSystemId; // TODO: ???
                     name = value.Name;
                     solarSystemId = value.SolarSystemId;
                     typeId = value.TypeId;
        }

    let toStargate (value: Stargate.Root) =
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

    let toMoon (value: Moon.Root) =
        {
            MoonData.id = value.MoonId;
                    name = value.Name;
                    solarSystemId = value.SystemId;
                    position = { PositionData.x = float value.Position.X; 
                                                    y = float value.Position.Y; 
                                                    z = float value.Position.Z};
        }

    let toBelt (value: AsteroidBelt.Root) = 
        {
            AsteroidBeltData.id = 0; // TODO:
                                name = value.Name;
                                solarSystemId = value.SystemId;
                                position = { PositionData.x = float value.Position.X; 
                                                            y = float value.Position.Y; 
                                                            z = float value.Position.Z};
        }

    let walkOverAll() =
        async {
            let! regions = EsiFiles.regions sourcePath |> Async.map (Seq.map toRegion >> Seq.length)
            
            let! constellations = EsiFiles.constellations sourcePath |> Async.map (Seq.map toConstellation >> Seq.length)
            
            let! solarSystems = EsiFiles.solarSystems sourcePath  |> Async.map (Seq.map toSolarSystem >> Seq.length)
            let! planets = EsiFiles.planets sourcePath |> Async.map (Seq.map toPlanet >> Seq.length )
            let! moons = EsiFiles.moons sourcePath |> Async.map (Seq.map toMoon >> Seq.length)
            let! stargates = EsiFiles.stargates sourcePath |> Async.map (Seq.map toStargate >> Seq.length)
            
            let! belts = EsiFiles.belts sourcePath |> Async.map (Seq.map toBelt >> Seq.length)
            let! stars = EsiFiles.stars sourcePath |> Async.map (Seq.map toStar >> Seq.length)
        
            return true
        }

    let startGeneration() =
        
        ConsoleUtils.info "Starting..."

        // TODO: 
        walkOverAll() |> Async.RunSynchronously |> ignore
        
        ConsoleUtils.info "Done."

        true

    member this.Start() = startGeneration()