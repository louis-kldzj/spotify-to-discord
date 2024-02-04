module SpotifyToWhatsApp.Commands

open System
open System.Threading
open Microsoft.FSharp.Reflection

let private createDatabase () = Data.DataBase.createDatabase ()

let private syncContributors () =
    Spotify.Query.getAllContributors () |> Set.iter Data.DataBase.insertContributor

let private listUsers () = Data.DataBase.listContributors ()

let private syncTracks () =
    Spotify.Query.getAllTracksAsModel () |> Data.DataBase.insertTracks

let private sendTestMessage () = Discord.Webhook.sendTestMessage ()

let private sendTestTrackMessage () =
    let latest = Spotify.Query.getLatestTrack ()
    Discord.Webhook.sendSongAddedMessage latest

let private registerNewSong (newTrack: Spotify.Model.Item) =
    let dbTrack = Spotify.Query.spotifyItemToTrack newTrack
    Data.DataBase.insertTrack null dbTrack
    Discord.Webhook.sendSongAddedMessage newTrack

let private checkLatestSong (total: int) =
    let newTotal, latest = Spotify.Query.getLatestNewTracks total

    if latest.items |> isNull || latest.items.Length = 0 then
        printfn "No new song detected"
    else
        latest.items
        |> Array.iter (fun newTrack ->
            if Data.DataBase.doesTrackExist newTrack.track.id then
                printfn "Track already exists"
            else
                printfn "New track registered"
                registerNewSong newTrack)

    newTotal

let rec private listenForNewSongs (waitTime: int) (total: int) : unit =
    Thread.Sleep(TimeSpan.FromSeconds(waitTime))
    let newTotal = checkLatestSong (total)
    listenForNewSongs waitTime newTotal

let private startListeningForNewSongs () =
    let total = Spotify.Query.getCurrenttotal ()
    listenForNewSongs 10 total

type CommandArg(arg: string) =
    inherit System.Attribute()
    member this.Arg = arg



type Command =
    | [<CommandArg("N/A")>] Unrecognized of string
    | [<CommandArg("help - display this screen")>] Help
    | [<CommandArg("exit - exit the application")>] Exit
    | [<CommandArg("create-db - create database")>] CreateDb
    | [<CommandArg("sync-users - sync users between Spotify and DB")>] SyncUsers
    | [<CommandArg("list-users - list users in DB")>] ListUsers
    | [<CommandArg("sync-tracks - sync tracks between Spotify and DB")>] SyncTracks
    | [<CommandArg("sync-db - sync users and tracks between Spotify and DB")>] SyncDb
    | [<CommandArg("send-test - sends a test message to discord")>] SendTestMessage
    | [<CommandArg("send-test-track - send a test track added message to discord")>] SendTestTrackAddedMessage
    | ListenForNewSongs

let commandToArgument =
    function
    | Command.Exit -> "exit"
    | Command.Help -> "help"
    | Command.ListUsers -> "list-users"
    | Command.CreateDb -> "create-db"
    | Command.SyncUsers -> "sync-users"
    | Command.SyncTracks -> "sync-tracks"
    | Command.SyncDb -> "sync-db"
    | Command.SendTestMessage -> "send-test"
    | Command.SendTestTrackAddedMessage -> "send-test-track"
    | Command.ListenForNewSongs -> "listen"

let allArguments =
    seq {
        for command in FSharpType.GetUnionCases(typeof<Command>) do
            let (case, _) = FSharpValue.GetUnionFields(command.Name, command.DeclaringType)

            for attribute in case.GetCustomAttributes() do
                if attribute = typeof<CommandArg> then
                    yield (attribute :?> CommandArg).Arg
                else
                    yield ""
    }
    |> Array.ofSeq

let argumentToCommand =
    function
    | "exit" -> Command.Exit
    | "help" -> Command.Help
    | "list-users" -> Command.ListUsers
    | "create-db" -> Command.CreateDb
    | "sync-users" -> Command.SyncUsers
    | "sync-tracks" -> Command.SyncTracks
    | "sync-db" -> Command.SyncDb
    | "send-test" -> Command.SendTestMessage
    | "send-test-track" -> Command.SendTestTrackAddedMessage
    | "listen" -> Command.ListenForNewSongs
    | a -> Command.Unrecognized(a)

let help () =
    allArguments |> Array.iter (fun h -> printfn $"{h}")

let commandToAction =
    function
    | Command.Unrecognized arg -> printfn $"Unrecognized command {arg}"
    | Command.Exit -> exit (0)
    | Command.Help -> printfn "Nothing yet"
    | Command.CreateDb -> createDatabase ()
    | Command.SyncUsers -> syncContributors ()
    | Command.ListUsers -> listUsers ()
    | Command.SyncTracks -> syncTracks ()
    | Command.SyncDb ->
        syncContributors ()
        syncTracks ()
    | Command.SendTestMessage -> sendTestMessage ()
    | Command.SendTestTrackAddedMessage -> sendTestTrackMessage ()
    | Command.ListenForNewSongs -> startListeningForNewSongs ()

let commandToActionDebug (command: Command) =
    printfn $"Running {command.ToString()} command"
    commandToAction command
