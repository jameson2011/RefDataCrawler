namespace RefDataCrawler

open System
open System.Reflection


type PartitionedModuleSource = 
    {
        partition:          int;
        namespaceName:      string;
        moduleName:         string;
        importedNamespaces: string[];
        funcName:           string;
        sourceLines:        string[];
    }

type ModuleSource = 
    {
        partition:          int;
        filePath:           string option;
        namespaceName:      string;
        moduleName:         string;
        funcName:           string;
        source:             string;
    }


module FSharpSource=
    open System.Xml.Linq
    
    let private intOptionType = (Some 1).GetType()
    let private floatOptionType = (Some 1.).GetType()

    let indent count value =
        new System.String(' ', 2 * count) + value

    let getPropertyInfos (t: Type) =
        t.GetProperties() |> Seq.filter (fun pi -> pi.CanRead) 

    let getPropertyValues (value) = 
        let getPropertyValue value (prop: PropertyInfo) =
            let m = prop.GetGetMethod()
            let v = m.Invoke(value, [||])
            ( prop.Name, v)

        value.GetType() |> getPropertyInfos
                        |> Seq.map (getPropertyValue value)
                        |> List.ofSeq
    
    let typeAlias (t: Type) =
        match t with
        | x when x = typedefof<int> ->          "int"
        | x when x = typedefof<string> ->       "string"
        | x when x = typedefof<int64> ->        "int64"
        | x when x = typedefof<float> ->        "float"
        | x when x = typedefof<bool> ->         "bool"
        | x when x = typedefof<int[]> ->        "int[]"
        | x when x = typedefof<int64[]> ->      "int64[]"
        | x when x = typedefof<string[]> ->     "string[]"
        | x when x = intOptionType ->           "int option"
        | x when x = floatOptionType ->         "float option"
        | x when x = typedefof<DogmaAttributeValueData[]> -> "DogmaAttributeValueData[]"
        | x when x = typedefof<DogmaEffectValueData[]> ->    "DogmaEffectValueData[]"
        | x -> x.Name
        
    let partitionEntitiesById bucketCount (id: 'a -> int) (values: seq<'a>) =
        let bucket value = (id value) % bucketCount 
        let map value = (bucket value, value)

        values |> Seq.map map 

    let toGenEntityFunction functionName (id: 'a -> int) (source: 'a -> string) (values: seq<'a>) =
        let getCase value =
            let id = id value
            source value |> sprintf "| %i -> %s |> Some" id
            
        seq {
            yield sprintf "let %s id = " functionName
            yield "match id with " |> indent 1
            yield! values |> Seq.map (getCase >> indent 1) 
            yield "| _ -> None" |> indent 1
            } |> Array.ofSeq

    let toGenMapFunction functionName bucketCount (values: seq<int * string * string>) =
        seq {
                yield sprintf "let %s id = " functionName
                yield sprintf "let bkt = id %% %i" bucketCount |> indent 1
                yield "match bkt with" |> indent 1
                yield! values |> Seq.map (fun (bkt,modName,funcName) -> sprintf "| %i -> %s.%s id" bkt modName funcName |> indent 2) 
                yield "| _ -> None" |> indent 2
            } |> List.ofSeq
        
    let toEntityEnumerator functionName accessorFunctionName (ids: seq<int>)=
        let ids = ids |> Seq.map string |> String.concatenate "; " |> sprintf " [| %s |] "
        seq {
            yield sprintf "let %s =" functionName
            yield sprintf "let ids = %s" ids |> indent 2
            yield sprintf "(fun () -> ids |> Seq.map %s |> Seq.filter Option.isSome |> Seq.map Option.get )" accessorFunctionName |> indent 2
        } |> List.ofSeq

        
        
    let toRecordSource (recordType: Type) =
        let attrs = match recordType.GetCustomAttributes<StructAttribute>() |> Seq.tryHead with
                    | Some _ -> "[<Struct>]" + Environment.NewLine
                    | _ -> ""

        let property (pi: PropertyInfo) =
            (pi.Name, (pi.PropertyType |> typeAlias) )
        
        let props = recordType  |> getPropertyInfos
                                |> Seq.map property
                                |> Seq.map (fun (n,t) -> sprintf "%s: %s" n t)
                                |> String.concatenate "; "

        sprintf "%stype %s = { %s }" attrs recordType.Name props

    let toUnionSource (unionType: Type) =
        let cases = Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(unionType)
                    |> Seq.map (fun c -> c.Name |> sprintf "| %s")
        seq {
            yield sprintf "type %s = " unionType.Name
            yield! cases
        } |> String.concatenate Environment.NewLine

    let rec toRecordInstance (value) =
                        
        let rec valueString (value: Object) = 
            let arrayValues values = values |> Seq.map valueString |> String.concatenate "; " |> sprintf "[| %s |]"
            
            match value with
            | :? int32 as x ->              string x
            | :? int64 as x ->              (string x) + "L" 
            | :? string as x ->             sprintf "\"%s\"" x
            | :? float as x ->              sprintf "%f" x
            | :? (int[]) as xs ->           arrayValues xs
            | :? (string[]) as xs ->        arrayValues xs
            | :? bool as x ->               string x |> String.lower
            | :? (PlanetRefData[]) as xs -> arrayValues xs
            | :? (DogmaAttributeValueData[]) as xs -> arrayValues xs
            | :? (DogmaEffectValueData[]) as xs -> arrayValues xs
            | :? PlanetRefData as pd ->     sprintf "{ PlanetRefData.planetId = %i; moonIds=%s; beltIds= %s }" pd.planetId (arrayValues pd.moonIds) (arrayValues pd.beltIds)
            | :? DogmaAttributeValueData as vd ->
                                            sprintf "{ DogmaAttributeValueData.attributeId = %i; value= %f }" vd.attributeId vd.value
            | :? DogmaEffectValueData as vd->
                                            sprintf "{ DogmaEffectValueData.effectId = %i; isDefault = %b }" vd.effectId vd.isDefault
            | null ->                       "None"
            | _ -> (string value).Replace('\n', ' ').Replace('\r', ' ')

        
        let typeName = value.GetType().Name
        
        let propValues = value  |> getPropertyValues
                                |> Seq.map (fun (n,v) -> (n, valueString v)) 
                                |> List.ofSeq
        let propValuesSource = propValues |> Seq.map (fun (n,v) -> sprintf "%s= %s" n v) |> List.ofSeq

        let result = sprintf "{ %s.%s }" typeName (propValuesSource |> String.concatenate "; ")
        
        result

    let toModule namespaceName internalAccess moduleName (importedNamespaces:seq<string>) (source: seq<string>) =
        seq {
                yield (sprintf "namespace %s" namespaceName)
                
                yield "open System"
                yield! importedNamespaces |> Seq.map (sprintf "open %s")
                yield (sprintf "module %s %s=" (if internalAccess then "internal" else "") moduleName)
        
                yield! source |> Seq.map (indent 2)
            }

    let toTypeDefs namespaceName (definitions) =
        seq {
                yield (sprintf "namespace %s" namespaceName)
                yield "open System"
        
                yield! definitions 
            }

    let writeSource folder filename source =
        async {

            let filePath = filename |> sprintf "%s.fs" |> Io.path folder

            do! Io.writeString filePath source

            return filePath
        }

    
    let moduleName prefix id = (sprintf "%s%i" prefix id)

    
    let partitionValues (partition: 'a -> int) partitions (values: seq<'a>)=
        values  |> partitionEntitiesById partitions partition
                |> Seq.groupBy fst
                |> Seq.map (fun (bkt,xs) ->   (bkt, xs |> Seq.map snd |> Array.ofSeq))
                |> Seq.sortBy (fun (bkt,_) -> bkt)

    let toPartitionedModuleFuncs (partition: 'a -> int) namespaceName funcModulePrefix funcName (importedNamespaces: seq<string>) (values: seq<int * 'a[]>)=
        values  |> Seq.map (fun (bkt,xs) -> { PartitionedModuleSource.partition = bkt;
                                                             namespaceName = namespaceName;
                                                             importedNamespaces = importedNamespaces |> Array.ofSeq;
                                                             moduleName = moduleName funcModulePrefix bkt;
                                                             funcName = funcName;
                                                             sourceLines = xs |> toGenEntityFunction funcName partition toRecordInstance;
                                                             })

    let toModuleSource  (values: seq<PartitionedModuleSource>) =
            let modules = 
                values          |> Seq.map (fun pms -> let src = toModule pms.namespaceName true pms.moduleName pms.importedNamespaces pms.sourceLines
                                                                            |> String.concatenate Environment.NewLine
                                                       { ModuleSource.filePath = None;
                                                                      partition = pms.partition;
                                                                      source = src;
                                                                      namespaceName = pms.namespaceName;
                                                                      moduleName = pms.moduleName;
                                                                      funcName = pms.funcName}
                                                       )

            modules

    let writeModuleSources folder (values: seq<ModuleSource>) =
        
        let modules = values   |> Seq.map (fun v -> let moduleFilePath = writeSource folder v.moduleName v.source 
                                                                        |> Async.RunSynchronously
                                                    { v with filePath = Some moduleFilePath }                                                    
                                                    )
                                |> Array.ofSeq
        modules
        
    let genAssemblyInfo namespaceName =
        seq {
            yield sprintf "namespace %s" namespaceName
            yield "open System.Reflection"
            yield @"[<assembly: AssemblyConfiguration("""")>]"
            yield @"[<assembly: AssemblyCompany("")>]"
            yield sprintf @"[<assembly: AssemblyProduct(""%s"")>]" namespaceName
            yield @"[<assembly: AssemblyCopyright(""Copyright © Jameson2011 2019 Data Copyright © CCP hf 2019"")>]"
            yield @"[<assembly: AssemblyTrademark("""")>]"
            yield @"[<assembly: AssemblyCulture("""")>]"
            yield @"[<assembly:System.Resources.NeutralResourcesLanguage(""en"")>]"
            yield @"[<assembly: AssemblyVersion(""0.0.0.0"")>]"
            yield @"[<assembly: AssemblyFileVersion(""0.0.0.0"")>]"
            yield @"[<assembly: AssemblyInformationalVersion("""")>]"
            yield @"do"
            yield @"    ()"
        } |> List.ofSeq

    let genEntityMapFunctions partitions funcName (modules: seq<ModuleSource>) =
        modules |> Seq.map (fun ms -> (ms.partition, ms.moduleName, ms.funcName))
                                    |> toGenMapFunction funcName partitions
                                    |> List.ofSeq

    

    let genEntitiesSource folder sourcePartitions namespaceName (id: 'a -> int) funcName funcModulePrefix mapModule (mapFuncs: string list) (importedNamespaces: seq<string>) (values: seq<'a>) =
        async {
            mapModule |> sprintf "Generating %s" |> ConsoleUtils.info
            
            let moduleName prefix b = (sprintf "%s%i" prefix b)
            
            let moduleSources = values  |> partitionValues id sourcePartitions 
                                        |> toPartitionedModuleFuncs id namespaceName funcModulePrefix funcName importedNamespaces
                                        |> toModuleSource 
                                        |> writeModuleSources folder
                                          
            let moduleFilePaths = moduleSources 
                                        |> Seq.map (fun ms -> ms.filePath) 
                                        |> Seq.reduceOptions
                                        |> Array.ofSeq

            
            let mapModuleFunction = moduleSources  
                                        |> genEntityMapFunctions sourcePartitions funcName
                                    
            let mapModuleFunctionsSource =
                                    [ mapModuleFunction; mapFuncs; ] 
                                        |> List.concat
                                        |> toModule namespaceName false mapModule importedNamespaces
                                        |> String.concatenate Environment.NewLine

            let mapModuleFilePath = mapModuleFunctionsSource
                                        |> writeSource folder mapModule
                                        |> Async.RunSynchronously

            return (mapModuleFilePath, moduleFilePaths)
        }

    let namespaceName namespacePrefix domain = sprintf "%s.%s" namespacePrefix domain

    let projectFileName namespaceName = sprintf "%s.fsproj" namespaceName

    let genProjectFile folder filename (topFilePaths: seq<string>) (dataFilePaths: seq<string>) (mapFilePaths: seq<string>) (includedProjects: seq<string>)=
        async {
            let filePath = filename |> Io.path folder
            
            let targetFramework = new XElement(XName.op_Implicit("TargetFramework"), "netstandard2.0")
            let propertyGroup = new XElement(XName.op_Implicit("PropertyGroup"), targetFramework)
            
            let fileIncludes = [ topFilePaths; dataFilePaths; mapFilePaths]
                                |> Seq.collect (fun fs -> fs)
                                |> Seq.map Io.filename
                                |> Seq.map (fun n -> new XElement(XName.op_Implicit("Compile"), new XAttribute(XName.op_Implicit("Include"), n)))
                                |> Array.ofSeq

            let fileItemGroup = new XElement(XName.op_Implicit("ItemGroup"), fileIncludes) 
            
            let paketInclude = new XElement(XName.op_Implicit("Import"), XAttribute(XName.op_Implicit("Project"), "..\..\.paket\Paket.Restore.targets"))
            
            let includedProjectRefs = includedProjects 
                                        |> Seq.map (fun p -> Io.relativePath filePath p ) 
                                        |> Seq.map (fun p -> new XElement(XName.op_Implicit("ProjectReference"), new XAttribute(XName.op_Implicit("Include"), p)))
                                        |> Array.ofSeq
            let includedProjectGroup = new XElement(XName.op_Implicit("ItemGroup"), includedProjectRefs ) 

            let proj = new XElement(XName.op_Implicit("Project"), 
                                    new XAttribute(XName.op_Implicit("Sdk"), "Microsoft.NET.Sdk"),
                                    propertyGroup, fileItemGroup, includedProjectGroup, paketInclude)
            
            
            

            proj.Save(filePath, SaveOptions.None)
            
            return filePath
        }