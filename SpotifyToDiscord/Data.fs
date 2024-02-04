module SpotifyToWhatsApp.Data

open System
open System.IO
open Microsoft.Data.Sqlite

module Model =

    type Contributor =
        { Id: string
          Name: string
          PhoneNumber: string }

    type Track =
        { Id: string
          Name: string
          Artist: string
          AddedBy: Contributor
          AddedOn: DateTime }

module Sql =

    let createDatabase = File.ReadAllText("schema.sql")

    let insertContributor = "INSERT INTO CONTRIBUTOR (Id) VALUES (@Username)"

    let selectAllContributors = "SELECT * FROM CONTRIBUTOR"

    let selectContributorName = "SELECT NAME FROM CONTRIBUTOR WHERE ID = @Id"

    let insertTrack =
        "INSERT INTO TRACK (ID, NAME, ARTIST, ADDED_BY, ADDED_ON) VALUES (@Id, @Name, @Artist, @AddedBy, @AddedOn) ON CONFLICT DO NOTHING"

    let selectTrack = "SELECT * FROM TRACK WHERE ID = @Id"



module DataBase =

    let private databasePath =
        Environment.GetEnvironmentVariable("ENV.THEREAL.SPOTIFY.DB_LOCATION")

    let private connectionString = @$"Data Source={databasePath}"

    let private createConnection () =
        printfn "Creating Connection"
        new SqliteConnection(connectionString)

    let private createOpenConnection () =
        let con = createConnection ()
        con.Open()
        con


    let private createCommand (connection: SqliteConnection) (commandText: string) =
        printfn $"Creating Command with text:\n%s{commandText}"
        new SqliteCommand(commandText, connection)

    let private executeNonQuery (command: SqliteCommand) =
        printfn "Executing command as non query"
        let returnCode = command.ExecuteNonQuery()
        printfn $"Executed with return code: {returnCode}"

    let createDatabase () =
        printfn "Creating Database"
        use connection = createOpenConnection ()
        use command = createCommand connection Sql.createDatabase
        executeNonQuery command
        printfn "Database Created"

    let insertContributor (id: string) =
        printfn $"Inserting Contributor: {id}"
        use connection = createOpenConnection ()
        use command = createCommand connection Sql.insertContributor
        command.Parameters.AddWithValue("@Username", id) |> ignore
        executeNonQuery command
        printfn "Contributor Inserted"

    let listContributors () =
        printfn "Listing all contributors:"
        use connection = createOpenConnection ()
        use command = createCommand connection Sql.selectAllContributors
        let reader = command.ExecuteReader()

        let missing = "MISSING"

        while reader.Read() do
            printfn "---------------------------------------"
            printfn $"Id: {reader.GetString(0).PadLeft(10)}"
            printfn $"Name: {(if reader.IsDBNull(1) then missing else reader.GetString(1)).PadLeft(8)}"
            printfn $"Phone Number: {if reader.IsDBNull(2) then missing else reader.GetString(2)}"

    let getContributorName (id: string) =
        printfn $"Fetching name for {id}"
        use connection = createOpenConnection ()
        use command = createCommand connection Sql.selectContributorName
        command.Parameters.AddWithValue("@Id", id) |> ignore
        let reader = command.ExecuteReader()
        reader.Read() |> ignore
        reader.GetString(0)




    let insertTrack (connection: SqliteConnection) (track: Spotify.Track) =
        let connection =
            if connection = null then
                createOpenConnection ()
            else
                connection

        let artists = track.artists |> String.concat ", "

        let trackId =
            if track.id = "" || isNull track.id then
                Guid.NewGuid().ToString()
            else
                track.id

        printfn $"--------Inserting track--------"
        printfn $"Id: {trackId}"
        printfn $"Name: {track.name}"
        printfn $"Artists: {artists}"
        printfn $"Added By: {track.added_by}"
        printfn $"Added On: {track.added_on}"


        use command = createCommand connection Sql.insertTrack
        command.Parameters.AddWithValue("@Id", trackId) |> ignore
        command.Parameters.AddWithValue("@Name", track.name) |> ignore
        command.Parameters.AddWithValue("@Artist", artists) |> ignore
        command.Parameters.AddWithValue("@AddedBy", track.added_by) |> ignore
        command.Parameters.AddWithValue("@AddedOn", track.added_on) |> ignore
        executeNonQuery command

    let insertTracks (tracks: Spotify.Track list) =
        printfn $"Inserting {tracks.Length} tracks"
        use connection = createOpenConnection ()
        tracks |> List.iter (insertTrack connection)
        printfn "Tracks Inserted"

    let doesTrackExist (id: string) =
        printfn $"Checking if track exists {id}"
        use connection = createOpenConnection ()
        use command = createCommand connection Sql.selectTrack
        command.Parameters.AddWithValue("@Id", id) |> ignore
        let reader = command.ExecuteReader()
        reader.Read()
