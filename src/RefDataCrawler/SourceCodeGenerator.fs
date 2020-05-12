namespace RefDataCrawler

open System

type SourceCodeGenerator(config: GenerateConfig)=

    let namespacePrefix = config.namespacePrefix
    let sourcePath = config.sourcePath
    let destinationPath = config.targetPath
    
    let isWormholeName (name: string) = 
        let prefixes = [ "A-R"; "B-R"; "C-R"; "D-R"; "E-R"; "F-R"; "G-R"; "H-R"; "K-R"]
        if prefixes |> Seq.filter (fun p -> name.StartsWith(p)) |> Seq.isEmpty |> not then
            name.Substring(3) |> Int32.TryParse |> fst
        else false

    let isAbyssalName (name: string) = 
        if name.StartsWith("ADR") || name.StartsWith("PR-") then
            name.Substring(3) |> Int32.TryParse |> fst
        else false
        

    let toSecurityRating regionName security=
        match security, isWormholeName regionName, isAbyssalName regionName with
        | x,_,_ when x >= 0.45 ->   SystemSecurity.Highsec
        | x,_,_ when x >= 0.0 ->    SystemSecurity.Lowsec
        | _,true,false ->           SystemSecurity.Wormhole 
        | _,false,true ->           SystemSecurity.Abyssal
        | _ ->                      SystemSecurity.Nullsec 

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

    let toSolarSystem (region: int -> RegionData) (id: string, value: SolarSystem.Root) =
        let region = region value.ConstellationId

        let planets = safeDefault (fun () -> value.Planets) [||]
                        |> Array.map (fun p -> { PlanetRefData.planetId = p.PlanetId;
                                                             moonIds = (safeDefault (fun () -> p.Moons) [||]);
                                                             beltIds = (safeDefault (fun () -> p.AsteroidBelts) [||]); 
                                                             } )
        let secStatus = float value.SecurityStatus
        { SolarSystemData.id = value.SystemId;
                        name = value.Name;
                        constellationId = value.ConstellationId;
                        position = { PositionData.x = float value.Position.X; 
                                                    y = float value.Position.Y; 
                                                    z = float value.Position.Z};
                        secRating = toSecurityRating region.name secStatus;
                        secClass = value.SecurityClass;
                        secStatus = secStatus;
                        starIds = safeDefault (fun () -> [| value.StarId |] ) [||] ;
                        planets = planets ;
                        stargateIds = safeDefault (fun () -> value.Stargates) [||];
                        stationIds = safeDefault (fun () -> value.Stations) [||];
                        }
        
    let toPlanet (getSystem: int -> SolarSystemData) (id: string, value: Planet.Root) =
        let system = getSystem value.SystemId

        let ref = system.planets |> Seq.find (fun pr -> pr.planetId = value.PlanetId )

        {
            PlanetData.id = value.PlanetId;
                       name = value.Name;
                       solarSystemId = value.SystemId;
                       position = { PositionData.x = float value.Position.X; 
                                                    y = float value.Position.Y; 
                                                    z = float value.Position.Z};
                       typeId = value.TypeId;
                       asteroidBeltIds = ref.beltIds;
                       moonIds = ref.moonIds;
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

    let toMoon (getPlanet: int -> PlanetData) (id: string, value: Moon.Root) =
        let planet = id |> Int32.Parse |> getPlanet
        {
            MoonData.id = value.MoonId;
                    name = value.Name;
                    solarSystemId = value.SystemId;
                    planetId = planet.id;
                    position = { PositionData.x = float value.Position.X; 
                                                    y = float value.Position.Y; 
                                                    z = float value.Position.Z};
        }

    let toBelt (getPlanet: int -> PlanetData) (id: string, value: AsteroidBelt.Root) = 
        let planet = id |> Int32.Parse |> getPlanet
        {
            AsteroidBeltData.id = Int32.Parse id;
                            name = value.Name;
                            solarSystemId = value.SystemId;
                            planetId = planet.id;
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
                            preExpression = safeDefault (fun () -> value.PreExpression) 0;
                            postExpression = safeDefault (fun () -> value.PostExpression) 0;
        }

    let toItemType (id: string, value: ItemType.Root) =
        
        let attrValue (v: ItemType.DogmaAttribute) = 
            {
                DogmaAttributeValueData.attributeId = v.AttributeId;
                    value = float v.Value;
            }
            
        let effectValue (v: ItemType.DogmaEffect) =
            {
                DogmaEffectValueData.effectId = v.EffectId;
                                    isDefault = v.IsDefault
            }

        {
            ItemTypeData.id = value.TypeId;
                         name = value.Name |> String.stripWhitespace |> String.escape ;
                         published = value.Published;
                         description = value.Description |> String.stripWhitespace |> String.escape ;
                         marketGroupId = safeDefault (fun () -> Some value.MarketGroupId) None;
                         groupId = value.GroupId;
                         dogmaAttributes =  value.DogmaAttributes |> Array.map attrValue;
                         dogmaEffects = value.DogmaEffects |> Array.map effectValue;
                         capacity = float value.Capacity;
                         graphicId = safeDefault (fun () -> Some value.GraphicId) None;
                         mass = float value.Mass;
                         packagedVolume = float value.PackagedVolume;
                         portionSize = value.PortionSize;
                         radius = float value.Radius;
                         volume = float value.Volume;
        }

    let toNpcCorp (id: string, value: NpcCorp.Root) =
        {
            NameData.id = Int32.Parse id;
                    name = value.Name;
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
                        typedefof<NameData>;
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

    let generateItemTypeSource namespaceName folder importedNamespaces (values: seq<ItemTypeData>) =
        async {
            let id (r: ItemTypeData) = r.id
            let funcName = "getItemType"
            let modulePrefix = "ItemTypes"
            let mapModule = modulePrefix
            
            
            let enumeratorFunction = values |> Seq.map id 
                                        |> FSharpSource.toEntityEnumerator "itemTypes" funcName 
                                        |> List.ofSeq

            return! values |> FSharpSource.genEntitiesSource folder config.sourcePartitions namespaceName id funcName modulePrefix mapModule enumeratorFunction importedNamespaces
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

    let generateNpcCorpSource namespaceName folder importedNamespaces (values: seq<NameData>) =
        async {
            let id (r: NameData) = r.id
            let funcName = "getNpcCorp"
            let modulePrefix = "NpcCorps"
            let mapModule = modulePrefix
            let partitions = 11
            let enumeratorFunction = values |> Seq.map id 
                                        |> FSharpSource.toEntityEnumerator "npcCorps" funcName 
                                        |> List.ofSeq

            return! values |> FSharpSource.genEntitiesSource folder partitions namespaceName id funcName modulePrefix mapModule enumeratorFunction importedNamespaces
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


    let generateMoons(domain: string) (importedNamespaces: seq<string>) (sharedTypesPath: string) (planets: PlanetData[])=
        async {
            let namespaceName = FSharpSource.namespaceName namespacePrefix domain
            let projectFileName = FSharpSource.projectFileName namespaceName

            let! rootFolder = namespaceName |> Io.path destinationPath |> Io.createFolder
            
            let getMoonPlanet (moonId: int)=
                planets |> Seq.filter (fun p -> p.moonIds |> Array.contains moonId)
                        |> Seq.head

            let! moons = EsiFiles.moons sourcePath |> Async.map (Seq.map (toMoon getMoonPlanet) >> Array.ofSeq)
            let! moonMapFile, moonDataFiles = moons |> generateMoonSource namespaceName rootFolder importedNamespaces

            
            let! projectFilePath = FSharpSource.genProjectFile rootFolder projectFileName  [] moonDataFiles [moonMapFile] [sharedTypesPath]

            return (namespaceName, projectFilePath)
        }

    let generateItemTypes (domain: string) (sharedTypesNamespaces: seq<string>) (sharedTypesPath: string) =
        async {
            let namespaceName = FSharpSource.namespaceName namespacePrefix domain
            let projectFileName = FSharpSource.projectFileName namespaceName

            let! rootFolder = namespaceName |> Io.path destinationPath |> Io.createFolder
            
            let! itemTypes = EsiFiles.itemTypes sourcePath |> Async.map (Seq.map toItemType >> Array.ofSeq)
            let! (itMapFile, itDataFiles) = generateItemTypeSource namespaceName rootFolder sharedTypesNamespaces itemTypes

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

            

            let mapFiles = [itMapFile; mgMapFile; cMapFile; gMapFile; daMapFile; deMapFile]
            let dataFiles = [| itDataFiles; mgDataFiles; cDataFiles; gDataFiles; daDataFiles; deDataFiles |] |> Array.collect id
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

            let getRegion id = regions |> Seq.find (fun r -> r.id = id) 

            let! constellations = EsiFiles.constellations sourcePath |> Async.map (Seq.map toConstellation >> Array.ofSeq)
            let! constellationMapFile, constellationDataFiles = constellations |> generateConstellationsSource namespaceName rootFolder sharedTypesNamespace

            let getConstellation id = constellations |> Seq.find (fun c -> c.id = id)
            let getConstellationRegion = (getConstellation >> (fun c -> c.regionId) >> getRegion)

            let! solarSystems = EsiFiles.solarSystems sourcePath  |> Async.map (Seq.map (toSolarSystem getConstellationRegion) >> Array.ofSeq)
            let! solarSystemMapFile, solarSystemDataFiles = solarSystems |> generateSolarSystemSource namespaceName rootFolder sharedTypesNamespace
            
            let getSystem id = solarSystems |> Seq.find (fun s -> s.id = id)

            let! planets = EsiFiles.planets sourcePath |> Async.map (Seq.map (toPlanet getSystem) >> Array.ofSeq )
            let! planetMapFile, planetDataFiles = planets |> generatePlanetSource namespaceName rootFolder sharedTypesNamespace

            let! stargates = EsiFiles.stargates sourcePath |> Async.map (Seq.map toStargate >> Array.ofSeq)
            let! stargateMapFile, stargateDataFiles = stargates |> generateStargateSource namespaceName rootFolder sharedTypesNamespace

            // TODO:
            let getBeltPlanet (beltId: int)=
                planets |> Seq.filter (fun p -> p.asteroidBeltIds |> Array.contains beltId)
                        |> Seq.head

            let! belts = EsiFiles.belts sourcePath |> Async.map (Seq.map (toBelt getBeltPlanet) >> Array.ofSeq)
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

            return (namespaceName, projectFilePath, planets)
        }

    let generateMiscData (domain: string) (sharedTypesNamespaces: seq<string>) (sharedTypesPath: string) =
        async {
            let namespaceName = FSharpSource.namespaceName namespacePrefix domain
            let projectFileName = FSharpSource.projectFileName namespaceName

            let! rootFolder = namespaceName |> Io.path destinationPath |> Io.createFolder
            
            let! npcCorps = EsiFiles.npcCorps sourcePath |> Async.map (Seq.map toNpcCorp >> Array.ofSeq)
            let! (ncMapFile, ncDataFiles) = generateNpcCorpSource namespaceName rootFolder sharedTypesNamespaces npcCorps
            
            let! projectFilePath = FSharpSource.genProjectFile rootFolder projectFileName  [] ncDataFiles [ncMapFile] [sharedTypesPath]

            return (namespaceName, projectFilePath)
            
        }

    let startGeneration() =
        
        "Starting..." |> ConsoleUtils.info 
        
        let start = DateTime.UtcNow
                
        let sharedTypesNamespace, sharedTypesProject = generateSharedTypes "Data.Entities" |> Async.RunSynchronously
        

        let _,_,planets = generateUniverse "Data.Universe" [sharedTypesNamespace] sharedTypesProject |> Async.RunSynchronously// |> ignore
        
        generateMoons "Data.Moons" [sharedTypesNamespace] sharedTypesProject planets |> Async.RunSynchronously |> ignore

        generateItemTypes "Data.ItemTypes" [sharedTypesNamespace] sharedTypesProject |> Async.RunSynchronously |> ignore

        generateMiscData "Data.Misc" [sharedTypesNamespace] sharedTypesProject |> Async.RunSynchronously |> ignore

        let duration = DateTime.UtcNow - start
        "Done. " + duration.ToString() |> ConsoleUtils.info

        true

    member this.Start() = startGeneration()