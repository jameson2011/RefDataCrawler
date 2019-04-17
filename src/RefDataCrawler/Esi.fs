namespace RefDataCrawler

open System.Net.Http

module Esi=

    
    let getUrl = HttpRoutes.url >> HttpRequests.get

    let regionIdsRequest () = 
        "v1/universe/regions/"  |> getUrl

    let regionRequest id = 
        id |> sprintf "v1/universe/regions/%s/"  |> getUrl
        
    let constellationIdsRequest() =
        "v1/universe/constellations/" |> getUrl

    let constellationRequest id =
        id |> sprintf "v1/universe/constellations/%s/" |> getUrl

    let systemIdsRequest() =
        "v1/universe/systems/" |> getUrl

    let systemRequest id =
        id  |> sprintf "v4/universe/systems/%s/" |> getUrl

    let planetRequest id = 
        id |> sprintf "v1/universe/planets/%s/" |> getUrl

    let asteroidBelt id =
        id |> sprintf "v1/universe/asteroid_belts/%s/" |> getUrl

    let stationRequest id =
        id |> sprintf "v1/universe/stations/%s/" |> getUrl

    let moonRequest id =
        id |> sprintf "v1/universe/moons/%s/" |> getUrl

    let stargateRequest id =
        id |> sprintf "v1/universe/stargates/%s/" |> getUrl

    let starRequest id =
        id |> sprintf "v1/universe/stars/%s/" |> getUrl
        
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

    let toSolarSystem (resp: WebResponse)=
        SolarSystem.Parse(resp.Message)

    let system client id =
        async {
            let! resp = id  |> systemRequest
                            |> HttpResponses.sendRequest client
            
            return match resp.Status with
                    | HttpStatus.OK -> resp |> toSolarSystem |> Some
                    | _ -> None // TODO:
        }
