namespace RefDataCrawler

open System
open System.Reflection

module SourceCodeGeneration=
    
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

    let toFSharpGenEntityFunction functionName (id: 'a -> int) (source: 'a -> string) (values: seq<'a>) =
        let getCase value =
            let id = id value
            source value |> sprintf "| %i -> %s |> Some" id
            
        seq {
            yield sprintf "let %s id = " functionName
            yield "match id with " |> indent 1
            yield! values |> Seq.map (getCase >> indent 1) 
            yield "| _ -> None" |> indent 1
            } |> Array.ofSeq

    let toFSharpGenMapFunction functionName bucketCount (values: seq<int * string * string>) =
        seq {
                yield sprintf "let %s id = " functionName
                yield sprintf "let bkt = id %% %i" bucketCount |> indent 1
                yield "match bkt with" |> indent 1
                yield! values |> Seq.map (fun (bkt,modName,funcName) -> sprintf "| %i -> %s.%s id" bkt modName funcName |> indent 2) 
                yield "| _ -> None" |> indent 2
            } |> List.ofSeq
        

    let toFSharpRecordSource (recordType: Type) =
        let property (pi: PropertyInfo) =
            (pi.Name, (pi.PropertyType |> typeAlias) )
        
        let props = recordType  |> getPropertyInfos
                                |> Seq.map property
                                |> Seq.map (fun (n,t) -> sprintf "%s: %s" n t)
                                |> String.concatenate "; "
        sprintf "type %s = { %s }" recordType.Name props

    let rec toFSharpRecordInstanceSource (value) =
                        
        let rec valueString (value: Object) = 
            let arrayValues values = values |> Seq.map valueString |> String.concatenate "; " |> sprintf "[| %s |]"
            
            match value with
            | :? int32 as x -> string x
            | :? int64 as x -> string x
            | :? string as x -> sprintf "\"%s\"" x
            | :? float as x -> sprintf "%f" x
            | :? (int[]) as xs -> arrayValues xs
            | :? (string[]) as xs -> arrayValues xs
            // TODO: nested object? PositionData is automatic...
            | _ -> (string value).Replace('\n', ' ').Replace('\r', ' ')

        
        let typeName = value.GetType().Name
        
        let propValues = value  |> getPropertyValues
                                |> Seq.map (fun (n,v) -> (n, valueString v)) 
                                |> List.ofSeq
        let propValuesSource = propValues |> Seq.map (fun (n,v) -> sprintf "%s= %s" n v) |> List.ofSeq

        let result = sprintf "{ %s.%s }" typeName (propValuesSource |> String.concatenate "; ")
        
        result

    let toFSharpModule namespaceName moduleName (definitions: seq<string>) =
        seq {
                yield (sprintf "namespace %s" namespaceName)
                yield "open System"
                yield (sprintf "module %s=" moduleName)
        
                yield! definitions |> Seq.map (indent 2)
            }

    let toFSharpTypeDefs namespaceName (definitions) =
        seq {
                yield (sprintf "namespace %s" namespaceName)
                yield "open System"
        
                yield! definitions 
            }

    let writeFSharpSource folder filename source =
        async {

            let filePath = filename |> sprintf "%s.fs" |> Io.path folder

            Io.deleteFile filePath
            do! Io.writeJson filePath source

            return filePath
        }

    let generateEntitiesSource folder sourcePartitions namespaceName (id: 'a -> int) funcName funcModulePrefix mapModule (values: seq<'a>) =
        async {

            // Below can be factored out
            let moduleName prefix b = (sprintf "%s%i" prefix b)
            
            let moduleFuncs = 
                    values    |> partitionEntitiesById sourcePartitions id
                              |> Seq.groupBy fst
                              |> Seq.map (fun (bkt,xs) ->   (bkt, xs |> Seq.map snd |> Array.ofSeq))
                              |> Seq.map (fun (bkt,xs) ->   let funcSource = xs |> toFSharpGenEntityFunction funcName id toFSharpRecordInstanceSource 
                                                            (bkt, (moduleName funcModulePrefix bkt), funcName, funcSource))
                              
                              
            
            let modules = 
                moduleFuncs 
                                |> Seq.map (fun (bkt, modName, funcName, source) -> let moduleSource = toFSharpModule namespaceName modName source  
                                                                                                            |> String.concatenate Environment.NewLine
                                                                                    let moduleFilePath = writeFSharpSource folder modName moduleSource 
                                                                                                            |> Async.RunSynchronously 
                                                                                    (bkt, modName, funcName, moduleFilePath) )
                                |> Array.ofSeq

            let moduleFilePaths = modules |> Seq.map (fun (_,_,_,path) -> path) |> List.ofSeq

            // generate a module that indexes all modules/functions
            let mapModuleFilePath = 
                            modules |> Seq.map (fun (bkt, modName, funcName, _) -> (bkt, modName, funcName))
                                    |> toFSharpGenMapFunction funcName sourcePartitions 
                                    |> toFSharpModule namespaceName mapModule 
                                    |> String.concatenate Environment.NewLine
                                    |> writeFSharpSource folder mapModule
                                    |> Async.RunSynchronously
                                    
            // return module files in correct project order
            let result = moduleFilePaths @ [ mapModuleFilePath ]
            return result
        }