namespace RefDataCrawler

module Universe=
    let regions () =
        
        let path = "v1/universe/regions/"
        let req = HttpRoutes.url path
                        |> HttpRequests.get
            
        []

