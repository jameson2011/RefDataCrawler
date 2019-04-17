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
type ServerStatus = JsonProvider<"./SampleServerStatus.json">
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
        ErrorLimit: int option;
        ErrorWindow: TimeSpan option;
        ETag: ETag option;
        Message: string;
    } with
    static member Ok retry etag message errorLimit errorWindow=
            {   WebResponse.Status = HttpStatus.OK;
                Retry = retry;
                Message = message;
                ErrorLimit = errorLimit;
                ErrorWindow = errorWindow;
                ETag = etag |> Option.map (fun t -> { ETag.tag = t });
            }
    static member OkNotModified retry etag errorLimit errorWindow= 
            {
                WebResponse.Status = HttpStatus.OkNotModified;
                Retry = retry;
                Message = "";
                ErrorLimit = errorLimit;
                ErrorWindow = errorWindow;
                ETag = etag |> Option.map (fun t -> { ETag.tag = t })
            }
    static member Unauthorized retry errorLimit errorWindow= 
            {   Status = HttpStatus.Unauthorized;
                Retry = retry;
                Message = "";
                ErrorLimit = errorLimit;
                ErrorWindow = errorWindow;
                ETag = None;
            }
    static member TooManyRequests retry errorLimit errorWindow= 
            {   Status = HttpStatus.TooManyRequests;
                Retry = retry;
                Message = "";
                ETag = None;
                ErrorLimit = errorLimit;
                ErrorWindow = errorWindow;
            }
    static member NotFound errorLimit errorWindow=
            {
                Status = HttpStatus.NotFound;
                Retry = None; //?
                Message = "";
                ETag = None;
                ErrorLimit = errorLimit;
                ErrorWindow = errorWindow;
            }
    static member Error retry msg errorLimit errorWindow = 
            {   Status = HttpStatus.Error;
                Retry = retry;
                Message = (sprintf "Error %s getting data" (msg.ToString()) );
                ETag = None;
                ErrorLimit = errorLimit;
                ErrorWindow = errorWindow;
            }

type CrawlerConfig = {
        targetPath: string;
    }