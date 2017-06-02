open System
open System.Text
open System.Globalization
open System.Configuration

#r "System.Net.Http.dll"
open System.Net.Http

#r @"FSharp.Data/lib/net40/FSharp.Data.dll"
open FSharp.Data

#r "Newtonsoft.Json"
open Newtonsoft.Json 

[<Literal>]
let sampleFile = __SOURCE_DIRECTORY__ + "/sample.json"

type Meetups = JsonProvider<sampleFile>
type Meetup = Meetups.Root

let meetupKey () =
    let appSettings = ConfigurationManager.AppSettings
    appSettings.["MeetupKey"]

let createRequest groupName = 
    meetupKey ()
    |> sprintf "http://api.meetup.com/%s/events?&sign=true&key=%s&photo-host=public&page=20&fields=short_link" groupName
    
let eventsFor groupName =
    groupName
    |> createRequest
    |> Meetups.Load

let date (m:Meetup) = 
    let epoch = System.DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)
    (m.Time + int64 m.UtcOffset) 
    |> float 
    |> epoch.AddMilliseconds

let culture = CultureInfo.CreateSpecificCulture("en-US")
let formatDate (d:DateTime) = d.ToString("ddd MMM d, HH:mm", culture)

let format (m:Meetup) =

    let venue = m.Venue
    let where = 
        match venue with
        | None -> "N/A"
        | Some(venue) -> 
            sprintf "%s, %s" venue.City venue.LocalizedCountryName
    
    let time = m |> date |> formatDate
    let title = m.Name
    let url = m.ShortLink

    sprintf """%s, %s: F# meetup "%s" %s #fsharp""" where time title url

type Message = { text:string }

let prepareMessage (text:string) =
    { text = text }
    |> JsonConvert.SerializeObject
    |> fun msg -> new StringContent(msg, Encoding.UTF8)

let Run(myTimer: TimerInfo, log: TraceWriter) =

    log.Info "Starting affiliates meetup report"

    let now = DateTime.Now
    let horizon = now.AddDays 60.0

    let message =
        [ 
            "sfsharp" 
            "Functional-Programming-in-F"
            "Cambridge-F-Community"
            "DC-fsharp"
            "fsharpsydney"
            "fsharp_tokyo"
            "FSharp-Toronto"
            "FSharping"
            "FSharp-Bogota"
            "FSharp-Quito"
        ]
        |> List.collect (eventsFor >> Seq.toList)
        |> List.sortBy (fun m -> m.Time)
        |> List.filter (fun m -> date m <= horizon)
        |> List.map format
        |> String.concat "\n"

    log.Info "Sending Slack Message"

    let client = new HttpClient()

    let appSettings = ConfigurationManager.AppSettings
    let url = appSettings.["SlackMoP"]

    message 
    |> prepareMessage
    |> fun msg -> client.PostAsync(url,msg) 
    |> ignore
