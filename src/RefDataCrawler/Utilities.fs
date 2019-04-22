namespace RefDataCrawler

open System


[<AutoOpen>]
module Operators=
    let (<-->) f g = (fun x -> [ f; g; ] |> List.iter (fun h -> h x ))

    let (?>) defaultValue map =
        try
            map()
        with
        | _ -> defaultValue

    let safeDefault map defaultValue =
        (?>) defaultValue map

module DateTimeOffset=

    let toUtc (x: DateTimeOffset) = x.UtcDateTime


module DateTime=
        
    let epochStart = DateTime(1970, 1, 1)

    let getUtcFromEpoch (seconds) = epochStart.AddSeconds(float seconds)

    let addTimeSpan (y: TimeSpan) (x: DateTime) = 
        x.Add(y)

    let diff (x: DateTime) (y: DateTime) = 
        x - y

    let date (x: DateTime) = 
        x.Date

    let ofDateTimeOffset (defaultTime: DateTime) (time: Nullable<DateTimeOffset>)=            
        if time.HasValue then
            time.Value.UtcDateTime
        else
            defaultTime
            
    let remoteTimeOffset (localUtcStart: DateTime) (localUtcEnd: DateTime) (remoteUtc: DateTime) =
        let localDuration = (localUtcEnd - localUtcStart).TotalMilliseconds / 2.
        let localUtc = localUtcStart.AddMilliseconds(localDuration)

        diff remoteUtc localUtc
        
module String =
    let concatenate delim (values: seq<string>) =
        System.String.Join(delim, values);

module Seq =
    let reduceOptions values =
        values  |> Seq.filter Option.isSome
                |> Seq.map Option.get

module Async =
    let map<'a, 'b> (map: 'a -> 'b) (value: Async<'a>) =
        async {
            let! r = value
            
            return map r
        }
