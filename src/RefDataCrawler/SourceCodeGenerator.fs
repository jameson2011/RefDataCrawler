﻿namespace RefDataCrawler

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
                        |> Array.map (fun p -> { PlanetRefData.planetId = p.PlanetId;
                                                             moonIds = (safeDefault (fun () -> p.Moons) [||]);
                                                             beltIds = (safeDefault (fun () -> p.AsteroidBelts) [||]); 
                                                             } )
        
        { SolarSystemData.id = value.SystemId;
                        name = value.Name;
                        constellationId = value.ConstellationId;
                        position = { PositionData.x = float value.Position.X; 
                                                    y = float value.Position.Y; 
                                                    z = float value.Position.Z};
                        secClass = value.SecurityClass;
                        secStatus = float value.SecurityStatus;
                        starIds = safeDefault (fun () -> [| value.StarId |] ) [||] ;
                        planetIds = planets ;// |> Array.map (fun p -> p.PlanetId);
                        //beltIds = belts;
                        //moonIds = moons;
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
                     age = value.Age;
                     luminosity = float value.Luminosity;
                     spectralClass = value.SpectralClass;
                     radius = value.Radius;
                     temperature = value.Temperature;
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

    let toMarketGroup (id: string, value: MarketGroup.Root) =
        {
            MarketGroupData.id = value.MarketGroupId;
                            name = value.Name;
                            description = String.stripWhitespace value.Description;
                            typeIds = value.Types;
                            parentMarketGroupId = safeDefault (fun () -> Some value.ParentGroupId) None;
        }

    let toCategory (id: string, value: Category.Root) =
        {
            CategoryData.id = value.CategoryId;
                         name = value.Name;
                         published = value.Published;
                         groupIds = value.Groups;
        }

    let toGroup (id: string, value: Group.Root) =
        {
            GroupData.id = value.GroupId;
                      name = value.Name;
                      categoryId = value.CategoryId;
                      published = value.Published;
                      typeIds = value.Types;
        }

    let toDogmaAttribute(id: string, value: DogmaAttribute.Root)=
        {
            DogmaAttributeData.id = value.AttributeId;
                               name = value.Name;
                               description = String.stripWhitespace value.Description;
                               published = safeDefault (fun () -> value.Published) false;
                               unitId = safeDefault (fun () -> Some value.UnitId ) None;
                               defaultValue = float value.DefaultValue;
                               stackable = safeDefault (fun () -> value.Stackable) false;
                               highIsGood = safeDefault (fun () -> value.HighIsGood) false;
        }

    let toDogmaEffect (id: string, value: DogmaEffect.Root) =
        {
            DogmaEffectData.id = value.EffectId;
                            name = value.Name;
                            description = String.stripWhitespace value.Description;
                            displayName = String.stripWhitespace value.DisplayName;
                            effectCategory = value.EffectCategory;
                            preExpression = value.PreExpression;
                            postExpression = value.PostExpression;
        }

    let generateTypeDefinitions namespaceName folder (types: seq<Type>) =
        async {
            
            let source t = match Types.isRecordType t, Types.isUnion t with
                            | _, true -> FSharpSource.toUnionSource t
                            | _, _ -> FSharpSource.toRecordSource t
                            

            let typeSources = 
                    types   |> Seq.map source
                            |> FSharpSource.toTypeDefs namespaceName
                            |> String.concatenate Environment.NewLine
                                        
            let! recordTypesPath = FSharpSource.writeSource folder "Entities" typeSources
            
            return recordTypesPath   
        }

    let generateUniverseTypes namespaceName folder =
        async {
            return! [   typedefof<PositionData>; typedefof<PlanetRefData>; typedefof<SystemSecurity>;
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

    let generateMarketGroupSource namespaceName folder importedNamespaces (values: seq<MarketGroupData>) =
        async {
            let id (r: MarketGroupData) = r.id
            let funcName = "getMarketGroup"
            let modulePrefix = "MarketGroups"
            let mapModule = modulePrefix

            
            let enumeratorFunction = values |> Seq.map id 
                                        |> FSharpSource.toEntityEnumerator "marketGroups" funcName 
                                        |> List.ofSeq

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule enumeratorFunction importedNamespaces
        }

    let generateCategorySource namespaceName folder importedNamespaces (values: seq<CategoryData>) =
        async {
            let id (r: CategoryData) = r.id
            let funcName = "getCategory"
            let modulePrefix = "Categories"
            let mapModule = modulePrefix

            
            let enumeratorFunction = values |> Seq.map id 
                                        |> FSharpSource.toEntityEnumerator "categories" funcName 
                                        |> List.ofSeq

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule enumeratorFunction importedNamespaces
        }

    let generateGroupsSource namespaceName folder importedNamespaces (values: seq<GroupData>) =
        async {
            let id (r: GroupData) = r.id
            let funcName = "getGroup"
            let modulePrefix = "Groups"
            let mapModule = modulePrefix

            
            let enumeratorFunction = values |> Seq.map id 
                                        |> FSharpSource.toEntityEnumerator "groups" funcName 
                                        |> List.ofSeq

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule enumeratorFunction importedNamespaces
        }

    
    let generateDogmaAttributesSource namespaceName folder importedNamespaces (values: seq<DogmaAttributeData>) =
        async {
            let id (r: DogmaAttributeData) = r.id
            let funcName = "getDogmaAttribute"
            let modulePrefix = "DogmaAttributes"
            let mapModule = modulePrefix

            
            let enumeratorFunction = values |> Seq.map id 
                                        |> FSharpSource.toEntityEnumerator "dogmaAttributes" funcName 
                                        |> List.ofSeq

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule enumeratorFunction importedNamespaces
        }

    
    let generateDogmaEffectsSource namespaceName folder importedNamespaces (values: seq<DogmaEffectData>) =
        async {
            let id (r: DogmaEffectData) = r.id
            let funcName = "getDogmaEffect"
            let modulePrefix = "DogmaEffects"
            let mapModule = modulePrefix

            

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule [] importedNamespaces
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

    let generateItemTypes (domain: string) (sharedTypesNamespaces: seq<string>) (sharedTypesPath: string) =
        async {
            let namespaceName = FSharpSource.namespaceName namespacePrefix domain
            let projectFileName = FSharpSource.projectFileName namespaceName

            let! rootFolder = namespaceName |> Io.path destinationPath |> Io.createFolder

            let! marketGroups = EsiFiles.marketGroups sourcePath |> Async.map (Seq.map toMarketGroup >> Array.ofSeq)
            let! (mgMapFile, mgDataFiles) = generateMarketGroupSource namespaceName rootFolder sharedTypesNamespaces marketGroups

            let! categories = EsiFiles.categories sourcePath |> Async.map (Seq.map toCategory >> Array.ofSeq)
            let! (cMapFile, cDataFiles) = generateCategorySource namespaceName rootFolder sharedTypesNamespaces categories
            
            let! groups = EsiFiles.groups sourcePath |> Async.map (Seq.map toGroup >> Array.ofSeq)
            let! (gMapFile, gDataFiles) = generateGroupsSource namespaceName rootFolder sharedTypesNamespaces groups
            
            let! dogmaAttrs = EsiFiles.dogmaAttributes sourcePath |> Async.map (Seq.map toDogmaAttribute >> Array.ofSeq)
            let! (daMapFile, daDataFiles) = generateDogmaAttributesSource namespaceName rootFolder sharedTypesNamespaces dogmaAttrs

            let! dogmaEffects = EsiFiles.dogmaEffect sourcePath |> Async.map (Seq.map toDogmaEffect >> Array.ofSeq)
            let! (deMapFile, deDataFiles) = generateDogmaEffectsSource namespaceName rootFolder sharedTypesNamespaces dogmaEffects


            let mapFiles = [mgMapFile; cMapFile; gMapFile; daMapFile; deMapFile]
            let dataFiles = [| mgDataFiles; cDataFiles; gDataFiles; daDataFiles; deDataFiles |] |> Array.collect id
            let! projectFilePath = FSharpSource.genProjectFile rootFolder projectFileName  [] dataFiles mapFiles [sharedTypesPath]

            return (namespaceName, projectFilePath)
            
        }

    let generateUniverse(domain: string) (sharedTypesNamespace: seq<string>) (sharedTypesPath: string)=
        async {
            let namespaceName = FSharpSource.namespaceName namespacePrefix domain
            let projectFileName = FSharpSource.projectFileName namespaceName

            let! rootFolder = namespaceName |> Io.path destinationPath |> Io.createFolder

            
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
        
        generateItemTypes "ItemTypes" [sharedTypesNamespace] sharedTypesProject |> Async.RunSynchronously |> ignore

        generateUniverse "Universe" [sharedTypesNamespace] sharedTypesProject |> Async.RunSynchronously |> ignore
        
        generateMoons "Moons" [sharedTypesNamespace] sharedTypesProject |> Async.RunSynchronously |> ignore

        let duration = DateTime.UtcNow - start
        "Done. " + duration.ToString() |> ConsoleUtils.info

        true

    member this.Start() = startGeneration()