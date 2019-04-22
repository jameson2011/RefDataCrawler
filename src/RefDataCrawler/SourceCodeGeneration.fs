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
            | :? float as x -> string x 
            | :? (int[]) as xs -> arrayValues xs
            | :? (string[]) as xs -> arrayValues xs
            // TODO: nested object? PositionData is automatic...
            | _ -> string value 

        
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
