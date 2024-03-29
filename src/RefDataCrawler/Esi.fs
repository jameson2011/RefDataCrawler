﻿namespace RefDataCrawler


module Esi=
    open System.Net.Http

    
    let private getUrl = HttpRoutes.url >> HttpRequests.get
    let private getUrlQuery path query = HttpRoutes.urlQuery path query |> HttpRequests.get
    
    let private mapRespObject mapper resp =
        match resp.Status with
        | HttpStatus.OK ->  mapper resp |> Choice1Of2
        | _ ->              resp |> Choice2Of2

    
    let private mapRespArray mapper resp =
        match resp.Status with
        | HttpStatus.OK ->  mapper resp |> Choice1Of2
        | _ ->              resp |> Choice2Of2



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
        getUrlQuery "latest/universe/groups/" [ ("page", string page) ]
            
    let groupRequest id =
        id |> sprintf "v1/universe/groups/%s/" |> getUrl

    let typeIdsRequest page = 
        getUrlQuery "latest/universe/types/" [ ("page", string page) ]

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

    let marketGroupIdsRequest() =
        "v1/markets/groups/" |> getUrl

    let marketGroupRequest id =
        id |> sprintf "v1/markets/groups/%s/" |> getUrl

    let npcCorpsRequest()=
        "latest/corporations/npccorps/" |> getUrl

    let corpRequest id =
        id |> sprintf "v4/corporations/%s/" |> getUrl

    let toServerStatus resp = 
        resp.Message |> ServerStatus.Parse
        

    let serverStatus client =
        async {
            let! resp = serverStatusRequest() |> HttpResponses.response client

            return resp |> mapRespObject toServerStatus
        }

    let private getEntityIds client req=
        async {
            let! resp = req() |> HttpResponses.response client
            
            return resp |> mapRespArray (fun r -> r.Message |> Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>)
        }

    
    let rec private getPagedEntityIds client (req: int -> HttpRequestMessage) page (ids: int[]) =
        async {             
            let! resp = req page |> HttpResponses.response client
            
            let pageResp = resp |> mapRespArray (fun r -> r.Message |> Newtonsoft.Json.JsonConvert.DeserializeObject<int[]> ) 
            let totalPages = resp.Pages |> Option.defaultValue 0
                            
            return! match pageResp with
                    | Choice1Of2 xs -> match xs with
                                        | [||] -> async { return Choice1Of2 ids}
                                        | xs -> let ids = Array.concat [ids; xs] 
                                                if page >= totalPages then async { return Choice1Of2 ids}
                                                else ids |> getPagedEntityIds client req (page+1) 
                    | Choice2Of2 _ -> async { return pageResp }
        }

    let regionIds client = getEntityIds client regionIdsRequest
        
    let constellationIds client = getEntityIds client constellationIdsRequest

    let systemIds client = getEntityIds client systemIdsRequest


    let groupIds client = getPagedEntityIds client groupIdsRequest 1 [||] 

    let typeIds client = getPagedEntityIds client typeIdsRequest 1 [||] 
    
    let categoryIds client = getEntityIds client categoryIdsRequest
    
    let dogmaAttributeIds client = getEntityIds client dogmaAttributeIdsRequest

    let dogmaEffectIds client = getEntityIds client dogmaEffectIdsRequest

    let marketGroupIds client = getEntityIds client marketGroupIdsRequest

    let npcCorpIds client = npcCorpsRequest |> getEntityIds client

    let toConstellation (resp: WebResponse)=
        Constellation.Parse(resp.Message)

    let toSolarSystem (resp: WebResponse)=
        SolarSystem.Parse(resp.Message)

        