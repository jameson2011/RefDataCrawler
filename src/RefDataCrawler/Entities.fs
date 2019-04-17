namespace RefDataCrawler

open System
open FSharp.Data

type ServerVersion = 
    {
        version: string
    }

type ETag =
    {
        tag: string;
    }

type SolarSystem = JsonProvider<"./SampleSolarSystem.json">

type HttpStatus =
    | OK
    | OkNotModified
    | TooManyRequests
    | Unauthorized
    | Error
    | NotFound
    
type WebResponse=
    {
        Status: HttpStatus;
        Retry: TimeSpan option;
        Message: string;
        // TODO: errors?
        ETag: ETag option
    } with
    static member Ok retry etag message =
            {   WebResponse.Status = HttpStatus.OK;
                Retry = retry;
                Message = message;
                ETag = etag |> Option.map (fun t -> { ETag.tag = t });
            }
    static member OkNotModified retry etag = 
            {
                WebResponse.Status = HttpStatus.OkNotModified;
                Retry = retry;
                Message = "";
                ETag = etag |> Option.map (fun t -> { ETag.tag = t })
            }
    static member Unauthorized retry = 
            {   Status = HttpStatus.Unauthorized;
                Retry = retry;
                Message = "";
                ETag = None;
            }
    static member TooManyRequests retry = 
            {   Status = HttpStatus.TooManyRequests;
                Retry = retry;
                Message = "";
                ETag = None;
            }
    static member NotFound =
            {
                Status = HttpStatus.NotFound;
                Retry = None;
                Message = "";
                ETag = None;
            }
    static member Error retry error = 
            {   Status = HttpStatus.Error;
                Retry = retry;
                Message = (sprintf "Error %s getting data" (error.ToString()) );
                ETag = None;
            }

type CrawlerConfig = {
        targetPath: string;
    }