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

    let stripWhitespace (value: string) =
        value.Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ' )
            .Replace('\"', ' ')
        
    let lower (value: string) = value.ToLower()

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

module Math =
    
    // source:http://www.fssnip.net/3X
    let allPrimes =
        let isPrime n =
            let sqrt' = (float >> sqrt >> int) n 
            [ 2 .. sqrt' ] 
            |> List.forall (fun x -> n % x <> 0) 
        let rec allPrimes' n =
            seq { 
                if isPrime n then
                    yield n
                yield! allPrimes' (n+1) 
            }
        allPrimes' 2
      
    let primeBefore limit = 
        let primes = allPrimes
        primes |> Seq.takeWhile (fun p -> p <= limit) |> Seq.rev |> Seq.head

