namespace RefDataCrawler

open System.Net.Http
open FSharp.Data

module Universe=

    type SolarSystem = JsonProvider<"./SampleSolarSystem.json">

    let regionIdsRequest () = 
        "v1/universe/regions/"  
            |> HttpRoutes.url
            |> HttpRequests.get

    let constellationIdsRequest() =
        "v1/universe/constellations/"
            |> HttpRoutes.url
            |> HttpRequests.get

    let systemIdsRequest() =
        "v1/universe/systems/"
            |> HttpRoutes.url
            |> HttpRequests.get

    let systemRequest id =
        id  |> sprintf "v4/universe/systems/%s/"
            |> HttpRoutes.url
            |> HttpRequests.get

    // TODO: these should just be path / request constructors...
    let regionIds client =
        async {

            
            let! resp = regionIdsRequest() |> HttpResponses.sendRequest client
            
            return match resp.Status with
                    | HttpStatus.OK -> resp.Message |> Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>
                    | _ -> [||] // TODO: exception!
        }
        
    
    let constellationIds client =
        async {
            let! resp = constellationIdsRequest() 
                            |> HttpResponses.sendRequest client
            
            return match resp.Status with
                    | HttpStatus.OK -> resp.Message |> Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>
                    | _ -> [||] // TODO:
        }

    let systemIds client =
        async {
        
            let! resp = systemIdsRequest() 
                            |> HttpResponses.sendRequest client
            
            return match resp.Status with
                    | HttpStatus.OK -> resp.Message |> Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>
                    | _ -> [||] // TODO:
        }

    
    let system client id =
        async {
            let! resp = id  |> systemRequest
                            |> HttpResponses.sendRequest client
            
            return match resp.Status with
                    | HttpStatus.OK -> SolarSystem.Parse(resp.Message) |> Some
                    | _ -> None // TODO:
        }
