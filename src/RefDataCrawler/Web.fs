namespace RefDataCrawler

open System
open System.Net
open System.Net.Http
open System.IO
open System.IO.Compression

module HttpConstants =
    [<Literal>]
    let esiHost = "https://esi.evetech.net"

    [<Literal>]
    let selectTranquility = "datasource=tranquility"


module HttpHeaders =
    [<Literal>]
    let userAgent = "jameson2011 (https://github.com/jameson2011)"

    [<Literal>]
    let gzip = "gzip"        
    
    [<Literal>]
    let eTag = "ETag"
    
    [<Literal>]
    let ifNoneMatch = "If-None-Match"    

    [<Literal>]
    let esiErrorLimitRemain = "X-Esi-Error-Limit-Remain"

    [<Literal>]
    let esiErrorLimitReset  = "X-Esi-Error-Limit-Reset"

module HttpRoutes =
    
    let url path =
        let ub = new UriBuilder(HttpConstants.esiHost)

        ub.Path <- path
        ub.Query <- HttpConstants.selectTranquility

        ub.Uri



module HttpRequests=
    
    let httpClient()=
        let client = new HttpClient()
        client.DefaultRequestHeaders.UserAgent.ParseAdd(HttpHeaders.userAgent)
        client.DefaultRequestHeaders.AcceptEncoding.Add(System.Net.Http.Headers.StringWithQualityHeaderValue(HttpHeaders.gzip))
        client
        
    let request (method: HttpMethod) (uri: Uri) = 
        new HttpRequestMessage(method, uri)
        
    let get (uri: Uri) = request HttpMethod.Get uri

    let post (uri: Uri) = request HttpMethod.Post uri

    let etag (etag: string) (request: HttpRequestMessage)  =
        request.Headers.Add(HttpHeaders.ifNoneMatch, [ etag ])
        request


module HttpResponses=
    
    let private decompressGzip(content: HttpContent)=
        async {
            use! stream = content.ReadAsStreamAsync() |> Async.AwaitTask
            use gzipStream = new GZipStream(stream, CompressionMode.Decompress)
            use rdr = new StreamReader(gzipStream)
            return! rdr.ReadToEndAsync() |> Async.AwaitTask
        }

    let private extractContent (content: HttpContent) =
            let isGzip = content.Headers.ContentEncoding
                            |> Seq.contains(HttpHeaders.gzip)
            match isGzip with
            | false -> content.ReadAsStringAsync() |> Async.AwaitTask
            | _ -> decompressGzip content

    let private getHeaderValue name (response: Http.HttpResponseMessage) = 
        response.Headers
            |> Seq.filter (fun h -> h.Key = name)
            |> Seq.collect (fun h -> h.Value)
            |> Seq.tryHead
                       
    
    let private getErrorLimitRemaining (response: HttpResponseMessage)=
        response 
            |> getHeaderValue HttpHeaders.esiErrorLimitRemain
            |> Option.bind (fun v -> match System.Int32.TryParse(v) with
                                     | true,x -> Some x
                                     | _ -> None )
            
    let private getErrorLimitReset (response: HttpResponseMessage) =
        response 
            |> getHeaderValue HttpHeaders.esiErrorLimitReset 
            |> Option.bind (fun v -> match System.Int32.TryParse(v) with
                                         | true,x -> float x |> TimeSpan.FromSeconds |> Some 
                                         | _ -> None )

    let private getAge (response: HttpResponseMessage)=
        Option.ofNullable response.Headers.Age 

    let private getServerTime (response: HttpResponseMessage)=
        let age = getAge response
        Option.ofNullable response.Headers.Date
            |> Option.map DateTimeOffset.toUtc
            |> Option.map2 DateTime.addTimeSpan age
                    
    let private getExpires (response: HttpResponseMessage)=
        Option.ofNullable response.Content.Headers.Expires 
            |> Option.map DateTimeOffset.toUtc
            
    let private getWait (response: HttpResponseMessage) =           
        let expires = getExpires response            
        getServerTime response
            |> Option.map2 (DateTime.diff) expires 
            |> Option.map (max TimeSpan.Zero)
            
    let private getEtag (response: HttpResponseMessage)=
        response |> getHeaderValue HttpHeaders.eTag
            

    let private parseOkResponse (resp: HttpResponseMessage) =
        async {
            use content = resp.Content
            let! s = extractContent content
            
            return
                {   Status = HttpStatus.OK;
                    Retry = (getWait resp);
                    Message = s;
                    ETag = getEtag resp |> Option.map (fun t -> { ETag.tag = t });
                    ErrorLimit =  (getErrorLimitRemaining resp);
                    ErrorWindow = (getErrorLimitReset resp);
                }
        }

    let private parseNotModifiedResponse resp = 
        async {

            return
                {   Status = HttpStatus.OkNotModified;
                    Retry = (getWait resp);
                    Message = "";
                    ETag = getEtag resp |> Option.map (fun t -> { ETag.tag = t });
                    ErrorLimit =  (getErrorLimitRemaining resp);
                    ErrorWindow = (getErrorLimitReset resp);
                }
        }
        
    let private parseUnauthResp resp = 
        async {
            return
                {   Status = HttpStatus.Unauthorized;
                    Retry = (getWait resp);
                    Message = "";
                    ETag = None;
                    ErrorLimit =  (getErrorLimitRemaining resp);
                    ErrorWindow = (getErrorLimitReset resp);
                }
            }
            
    let private parseTooManyRequests resp = 
        async {
            return
                {   Status = HttpStatus.TooManyRequests;
                    Retry = (getWait resp);
                    Message = "";
                    ETag = None;
                    ErrorLimit =  (getErrorLimitRemaining resp);
                    ErrorWindow = (getErrorLimitReset resp);
                }
        }

    let private parseErrorResp resp = 
        async {
            return
                {   Status = HttpStatus.Error;
                    Retry = (getWait resp);
                    Message = sprintf "Error %i getting data" (int resp.StatusCode);
                    ETag = None;
                    ErrorLimit =  (getErrorLimitRemaining resp);
                    ErrorWindow = (getErrorLimitReset resp);
                }
        }

    let private parseNotFoundResp resp =
        async {
            return
                {   Status = HttpStatus.NotFound;
                    Retry = (getWait resp);
                    Message = "";
                    ETag = None;
                    ErrorLimit =  (getErrorLimitRemaining resp);
                    ErrorWindow = (getErrorLimitReset resp);
                }
        }

        
    let response (client: HttpClient) (request: HttpRequestMessage) =
        async {
            use! resp = client.SendAsync(request) |> Async.AwaitTask
            
            let! result = match resp.StatusCode with
                            | HttpStatusCode.OK ->  parseOkResponse resp
                            | HttpStatusCode.NotModified -> parseNotModifiedResponse resp
                            | HttpStatusCode.Unauthorized -> parseUnauthResp resp
                            | HttpStatusCode.NotFound -> parseNotFoundResp resp 
                            | HttpStatusCode.TooManyRequests -> parseTooManyRequests resp
                            | _ -> parseErrorResp resp

            return result
            }
            

