namespace RefDataCrawler

open System
open System.Reflection


module FSharpSource=
    open System.Xml.Linq
    
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
        | x when x = typedefof<int> ->      "int"
        | x when x = typedefof<string> ->   "string"
        | x when x = typedefof<int64> ->    "int64"
        | x when x = typedefof<float> ->    "float"
        | x when x = typedefof<int[]> ->    "int[]"
        | x when x = typedefof<int64[]> ->  "int64[]"
        | x when x = typedefof<string[]> -> "string[]"
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
        let property (pi: PropertyInfo) =
            (pi.Name, (pi.PropertyType |> typeAlias) )
        
        let props = recordType  |> getPropertyInfos
                                |> Seq.map property
                                |> Seq.map (fun (n,t) -> sprintf "%s: %s" n t)
                                |> String.concatenate "; "
        sprintf "type %s = { %s }" recordType.Name props

    let rec toRecordInstance (value) =
                        
        let rec valueString (value: Object) = 
            let arrayValues values = values |> Seq.map valueString |> String.concatenate "; " |> sprintf "[| %s |]"
            
            match value with
            | :? int32 as x -> string x
            | :? int64 as x -> string x
            | :? string as x -> sprintf "\"%s\"" x
            | :? float as x -> sprintf "%f" x
            | :? (int[]) as xs -> arrayValues xs
            | :? (string[]) as xs -> arrayValues xs
            | _ -> (string value).Replace('\n', ' ').Replace('\r', ' ')

        
        let typeName = value.GetType().Name
        
        let propValues = value  |> getPropertyValues
                                |> Seq.map (fun (n,v) -> (n, valueString v)) 
                                |> List.ofSeq
        let propValuesSource = propValues |> Seq.map (fun (n,v) -> sprintf "%s= %s" n v) |> List.ofSeq

        let result = sprintf "{ %s.%s }" typeName (propValuesSource |> String.concatenate "; ")
        
        result

    let toModule namespaceName internalAccess moduleName (source: seq<string>) =
        seq {
                yield (sprintf "namespace %s" namespaceName)
                yield "open System"
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

    let genEntitiesSource folder sourcePartitions namespaceName (id: 'a -> int) funcName funcModulePrefix mapModule (mapFuncs: string list) (values: seq<'a>) =
        async {
            mapModule |> sprintf "Generating %s" |> ConsoleUtils.info
            
            let moduleName prefix b = (sprintf "%s%i" prefix b)
            
            let moduleFuncs = 
                    values    |> partitionEntitiesById sourcePartitions id
                              |> Seq.groupBy fst
                              |> Seq.map (fun (bkt,xs) ->   (bkt, xs |> Seq.map snd |> Array.ofSeq))
                              |> Seq.sortBy (fun (bkt,_) -> bkt)
                              |> Seq.map (fun (bkt,xs) ->   let funcSource = xs |> toGenEntityFunction funcName id toRecordInstance 
                                                            (bkt, (moduleName funcModulePrefix bkt), funcName, funcSource))
                              
                              
            
            let modules = 
                moduleFuncs 
                                |> Seq.map (fun (bkt, modName, funcName, source) -> let moduleSource = toModule namespaceName true modName source  
                                                                                                            |> String.concatenate Environment.NewLine
                                                                                    let moduleFilePath = writeSource folder modName moduleSource 
                                                                                                            |> Async.RunSynchronously 
                                                                                    (bkt, modName, funcName, moduleFilePath) )
                                |> Array.ofSeq

            let moduleFilePaths = modules |> Seq.map (fun (_,_,_,path) -> path) |> List.ofSeq

            // generate a module that indexes all modules/functions
            let mapModuleFunction = 
                            modules |> Seq.map (fun (bkt, modName, funcName, _) -> (bkt, modName, funcName))
                                    |> toGenMapFunction funcName sourcePartitions
                                    |> List.ofSeq
                                    
            let mapModuleFunctions =[ mapModuleFunction; mapFuncs ] 
                                    |> List.concat
                                    |> toModule namespaceName false mapModule 
                                    
            let mapModuleFilePath = mapModuleFunctions
                                    |> String.concatenate Environment.NewLine
                                    
                                    |> writeSource folder mapModule
                                    |> Async.RunSynchronously
            
            return (mapModuleFilePath, moduleFilePaths)
        }

    let namespaceName namespacePrefix domain = sprintf "%s.%s" namespacePrefix domain

    let projectFileName namespaceName = sprintf "%s.fsproj" namespaceName

    let genProjectFile folder filename (topFilePaths: seq<string>) (dataFilePaths: seq<string>) (mapFilePaths: seq<string>)=
        async {
            
            let targetFramework = new XElement(XName.op_Implicit("TargetFramework"), "netstandard2.0")
            let propertyGroup = new XElement(XName.op_Implicit("PropertyGroup"), targetFramework)
            
            let includes = [ topFilePaths; dataFilePaths; mapFilePaths]
                            |> Seq.collect (fun fs -> fs)
                            |> Seq.map Io.filename
                            |> Seq.map (fun n -> new XElement(XName.op_Implicit("Compile"), new XAttribute(XName.op_Implicit("Include"), n)))
                            |> Array.ofSeq

            let itemGroup = new XElement(XName.op_Implicit("ItemGroup"), includes) 

            let proj = new XElement(XName.op_Implicit("Project"), 
                                    new XAttribute(XName.op_Implicit("Sdk"), "Microsoft.NET.Sdk"),
                                    propertyGroup, itemGroup)
            
            
            let filePath = filename |> Io.path folder

            proj.Save(filePath, SaveOptions.None)
            
            return true
        }