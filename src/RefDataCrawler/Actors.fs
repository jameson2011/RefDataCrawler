namespace RefDataCrawler

open System

type ActorMessage =
| Trace of string
| Info of string
| Exception of string * Exception
| Error of string
| Warning of string

| ServerStatus

| Regions
| RegionIds of string[]

| Constellations
| ConstellationIds of string[]

| SolarSystems
| SolarSystemId of string
| StarIds of string[]
| PlanetIds of string[]
| MoonIds of string[]
| AsteroidBeltIds of string[]
| StationIds of string[]
| StargateIds of string[]

| Groups
| GroupIds of string[]
| Categories
| CategoryIds of string[]
| Types
| TypeIds of string[]
| DogmaAttributes
| DogmaAttributeIds of string[]
| DogmaEffects
| DogmaEffectIds of string[]

| DiscoveredEntities of string * string[] 
| Entity of string * string * string * string
| FinishedEntity of string * string
| EntityMetadata of string * string[] * AsyncReplyChannel<EntityMetadata option[]>

| CrawlStatus of AsyncReplyChannel<CrawlProgress>
| Ping of AsyncReplyChannel<unit>

type PostMessage= ActorMessage -> unit
type PostString = string -> unit
type MessageInbox = MailboxProcessor<ActorMessage>


module Actors = 
    let forwardMany  (getMsg: 'a -> ActorMessage) (posts: (PostMessage) list) value =
        let msg = getMsg value
        posts |> List.iter (fun p -> p msg)            

    let postException name (log: PostMessage) ex = (ActorMessage.Exception (name, ex)) |> log
    let postError (log: PostMessage) = ActorMessage.Error >> log
    let postTrace  (log: PostMessage) = ActorMessage.Trace >> log
    let postInfo (log: PostMessage) = ActorMessage.Info >> log
        


