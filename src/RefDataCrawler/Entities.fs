﻿namespace RefDataCrawler

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
type Region = JsonProvider<"./SampleRegion.json">
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
        etag: string;
    }

type HttpStatus =
    | OK
    | OkNotModified
    | TooManyRequests
    | Unauthorized
    | Error
    | BadGateway
    | NotFound
    | HttpRequestError
    
type WebResponse=
    {
        Status: HttpStatus;
        Retry: TimeSpan option;
        ErrorLimit: int option;
        ErrorWindow: TimeSpan option;
        ETag: ETag option;
        Message: string;
    } 
    
type CrawlerConfig = 
    {
        targetPath: string;
        
        crawlRegions: bool;
        crawlConstellations: bool;
        crawlSystems: bool;
        crawlGroups: bool;
        crawlCategories: bool;
        crawlTypes: bool;
        crawlDogmaAttributes: bool;
        crawlDogmaEffects: bool;
        crawlMarketGroups: bool;

        maxErrors: int;

        verboseLogging: bool;
        showProgressTicker: bool;
    } with
    static member TargetPathDefault = @".\data\"

type GenerateConfig = 
    {
        targetPath :        string;
        sourcePath :        string;
        namespacePrefix:    string;
        sourcePartitions:   int;
    } with
    static member TargetPathDefault = @".\src\"
    
type CrawlEntityTypeProgress = 
    {
        name: string;
        discovered: int;
        completed: int;
    }

type CrawlProgress= 
    {
        isComplete:     bool;
        isErrorLimited: bool;
        errorCount:     int;
        entityTypes:    CrawlEntityTypeProgress[];
    }
