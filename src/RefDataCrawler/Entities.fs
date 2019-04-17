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
    } 
    
type CrawlerConfig = {
        targetPath: string;
    } with
    static member TargetPathDefault = @"./data/"