namespace RefDataCrawler


module Esi=
    open System.Net.Http

    
    let private getUrl = HttpRoutes.url >> HttpRequests.get
    let private getUrlQuery path query = HttpRoutes.urlQuery path query |> HttpRequests.get

    let private mapOkSome mapper resp =
        match resp.Status with
        | HttpStatus.OK -> mapper resp |> Some
        | _ -> None

    let private mapOkArray mapper resp =
        match resp.Status with
        | HttpStatus.OK -> mapper resp
        | _ -> [||]


    let serverStatusRequest() =
        "v1/status" |> getUrl

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
        id |> sprintf "v4/universe/systems/%s/" |> getUrl

    let planetRequest id = 
        id |> sprintf "v1/universe/planets/%s/" |> getUrl

    let asteroidBeltRequest id =
        id |> sprintf "v1/universe/asteroid_belts/%s/" |> getUrl

    let stationRequest id =
        id |> sprintf "v2/universe/stations/%s/" |> getUrl

    let moonRequest id =
        id |> sprintf "v1/universe/moons/%s/" |> getUrl

    let stargateRequest id =
        id |> sprintf "v1/universe/stargates/%s/" |> getUrl

    let starRequest id =
        id |> sprintf "v1/universe/stars/%s/" |> getUrl
        
    let groupIdsRequest page =
        getUrlQuery "v1/universe/groups/" [ ("page", string page) ]
            
    let groupRequest id =
        id |> sprintf "v1/universe/groups/%s/" |> getUrl

    let typeIdsRequest page = 
        getUrlQuery "v1/universe/types/" [ ("page", string page) ]

    let typeRequest id =
        id |> sprintf "v3/universe/types/%s/" |> getUrl
    
    let categoryIdsRequest() =
        "v1/universe/categories/" |> getUrl

    let categoryRequest id =
        id |> sprintf "v1/universe/categories/%s/" |> getUrl

    let dogmaAttributeIdsRequest() =
        "v1/dogma/attributes/" |> getUrl

    let dogmaAttributeRequest id =
        id |> sprintf "v1/dogma/attributes/%s/" |> getUrl

    let dogmaEffectIdsRequest() =
        "v1/dogma/effects/" |> getUrl

    let dogmaEffectRequest id =
        id |> sprintf "v2/dogma/effects/%s/" |> getUrl

    let toServerStatus resp = 
        resp.Message |> ServerStatus.Parse
        

    let serverStatus client =
        async {
            let! resp = serverStatusRequest() |> HttpResponses.response client

            return resp |> mapOkSome toServerStatus
        }

    let private getEntityIds client req=
        async {
        
            // TODO: error handling! Retry?
            let! resp = req() |> HttpResponses.response client
            
            return resp |> mapOkArray (fun r -> r.Message |> Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>)
        }

    
    let rec private getPagedEntityIds client (req: int -> HttpRequestMessage) page (ids: int[]) =
        async {
                
            // TODO: error handling! Retry?
            let! resp = req page |> HttpResponses.response client
            
            let pageIds = resp |> mapOkArray (fun r -> r.Message |> Newtonsoft.Json.JsonConvert.DeserializeObject<int[]> ) 
                
            return! match pageIds with
                    | [||] -> async { return ids }
                    | xs -> Array.concat [ids;xs] |> getPagedEntityIds client req (page+1) 
            }


    let regionIds client = getEntityIds client regionIdsRequest
        
    let constellationIds client = getEntityIds client constellationIdsRequest

    let systemIds client = getEntityIds client systemIdsRequest


    let groupIds client = getPagedEntityIds client groupIdsRequest 1 [||] 

    let typeIds client = getPagedEntityIds client typeIdsRequest 1 [||] 
    
    let categoryIds client = getEntityIds client categoryIdsRequest
    
    let dogmaAttributeIds client = getEntityIds client dogmaAttributeIdsRequest

    let dogmaEffectIds client = getEntityIds client dogmaEffectIdsRequest

    let toConstellation (resp: WebResponse)=
        Constellation.Parse(resp.Message)

    let toSolarSystem (resp: WebResponse)=
        SolarSystem.Parse(resp.Message)

        