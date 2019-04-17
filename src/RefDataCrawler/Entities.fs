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
type Constellation = JsonProvider<"./SampleConstellation.json">
type SolarSystem = JsonProvider<"./SampleSolarSystem.json">
type Planet = JsonProvider<"./SamplePlanet.json">
type AsteroidBelt = JsonProvider<"./SampleAsteroidBelt.json">
type Moon = JsonProvider<"./SampleMoon.json">
type Stargate = JsonProvider<"./SampleStargate.json">
type Station = JsonProvider<"./SampleStation.json">
type Star = JsonProvider<"./SampleStar.json">

type EntityMetadata = {
        entityType: string;
        id: string;
        captured: DateTimeOffset;
        etag: string;
    }

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
    static member TargetPathDefault = @".\data\"

type CrawlStatus = {
    name: string;
    discovered: int;
    completed: int;
    }