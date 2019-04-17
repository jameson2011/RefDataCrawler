namespace RefDataCrawler

open System

type ActorMessage =
| Trace of string
| Info of string
| Exception of string * Exception
| Error of string
| Warning of string

| RegionIds
| RegionId of string

| ConstellationIds
| ConstellationId of string

| SystemIds
| SystemId of string
| StarIds of string
| PlanetIds of string
| MoonIds of string
| AsteroidBeltIds of string
| StationIds of string
| StargateIds of string

| DiscoveredEntity of string * string
| Entity of string * string * string * string
| FinishedEntity of string * string
| CrawlStatus

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
        


