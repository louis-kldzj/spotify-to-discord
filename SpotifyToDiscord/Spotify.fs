module SpotifyToWhatsApp.Spotify

open System
open System.IO
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open Net

type Track =
    { id: string
      name: string
      artists: string array
      added_by: string
      added_on: DateTime }

module Model =

    type ExternalUrls = { spotify: string }

    type AddedBy =
        { [<JsonPropertyName("external_url")>]
          externalUrls: ExternalUrls
          href: string
          id: string
          [<JsonPropertyName("type")>]
          type_: string
          uri: string }

    type VideoThumbnail = { URL: obj }

    type ExternalIds = { isrc: string }

    type Track =
        { album: Album
          artists: Artist[]
          [<JsonPropertyName("available_markets")>]
          availableMarkets: string[]
          discNumber: int
          durationMs: int
          episode: bool
          explicit: bool
          externalIds: ExternalIds
          [<JsonPropertyName("external_urls")>]
          externalUrls: ExternalUrls
          href: string
          id: string
          isLocal: bool
          name: string
          popularity: int
          [<JsonPropertyName("preview_url")>]
          previewUrl: string
          track: bool
          [<JsonPropertyName("track_number")>]
          trackNumber: int
          [<JsonPropertyName("type")>]
          type_: string
          uri: string }

    and Album =
        { [<JsonPropertyName("album_type")>]
          albumType: string
          artists: Artist[]
          [<JsonPropertyName("available_markets")>]
          availableMarkets: string[]
          [<JsonPropertyName("external_urls")>]
          externalUrls: ExternalUrls
          href: string
          id: string
          images: Image[]
          name: string
          [<JsonPropertyName("release_date")>]
          releaseDate: string
          [<JsonPropertyName("release_date_precision")>]
          releaseDatePrecision: string
          [<JsonPropertyName("total_tracks")>]
          totalTracks: int
          [<JsonPropertyName("type")>]
          type_: string
          uri: string }

    and Image =
        { height: int; url: string; width: int }

    and Artist =
        { [<JsonPropertyName("external_urls")>]
          externalUrls: ExternalUrls
          href: string
          id: string
          name: string
          [<JsonPropertyName("type")>]
          type_: string
          uri: string }

    type Item =
        { [<JsonPropertyName("added_at")>]
          addedAt: DateTime
          [<JsonPropertyName("added_by")>]
          addedBy: AddedBy
          [<JsonPropertyName("is_local")>]
          isLocal: bool
          [<JsonPropertyName("type")>]
          primaryColor: obj
          track: Track
          [<JsonPropertyName("video_thumbnail")>]
          videoThumbnail: VideoThumbnail }

    type PlaylistResponse =
        { href: string
          items: Item[]
          limit: int
          next: string
          offset: int
          previous: obj
          total: int }



module Authorization =
    let clientId = Environment.GetEnvironmentVariable("ENV.THEREAL.SPOTIFY.CLIENT_ID")

    let clientSecret =
        Environment.GetEnvironmentVariable("ENV.THEREAL.SPOTIFY.CLIENT_SECRET")

    let endpoint = "https://accounts.spotify.com/api/token"
    let requestContent = "grant_type=client_credentials"

    let authorizationHeader =
        let raw = $"{clientId}:{clientSecret}"
        let bytes = Encoding.UTF8.GetBytes(raw)
        System.Convert.ToBase64String(bytes)

    let getAccessToken () =

        let request = new HttpRequestMessage(HttpMethod.Post, endpoint)

        request.Headers.Authorization <- AuthenticationHeaderValue("Basic", authorizationHeader)
        request.Content <- new StringContent(requestContent, Encoding.UTF8, "application/x-www-form-urlencoded")

        let response = sendRequest request |> Async.RunSynchronously


        let content =
            response.Content.ReadAsStringAsync().Result
            |> JsonSerializer.Deserialize<Map<string, obj>>

        content["access_token"] |> string

module Query =
    let private playlistId =
        Environment.GetEnvironmentVariable("ENV.THEREAL.SPOTIFY.PLAYLIST_ID")

    let private endpoint = $"https://api.spotify.com/v1/playlists/{playlistId}/tracks"

    let mutable private cache: Map<string, Model.PlaylistResponse> = Map.empty

    let private parseResponse (response: HttpResponseMessage) : Model.PlaylistResponse =
        let content = response.Content.ReadAsStringAsync().Result
        content |> JsonSerializer.Deserialize<Model.PlaylistResponse>

    let private getPlaylist (request: HttpRequestMessage) : Model.PlaylistResponse =
        if cache.ContainsKey(request.RequestUri.ToString()) then
            printfn $"Fetching from cache {request.RequestUri}"
            cache.[request.RequestUri.ToString()]
        else
            let response = sendRequest request |> Async.RunSynchronously
            let playlist = response |> parseResponse
            printfn $"Adding to cache {request.RequestUri}"
            cache <- cache.Add(request.RequestUri.ToString(), playlist)
            playlist

    let private getPlaylistIgnoreCache (request: HttpRequestMessage) : Model.PlaylistResponse =
        let response = sendRequest request |> Async.RunSynchronously
        let playlist = response |> parseResponse
        printfn $"Adding to cache {request.RequestUri}"
        playlist

    let private playlistRequest (accessToken: string) : HttpRequestMessage =
        let request = new HttpRequestMessage(HttpMethod.Get, endpoint)
        request.Headers.Authorization <- AuthenticationHeaderValue("Bearer", accessToken)
        request

    let private getPlaylistTracks (accessToken: string) : Model.PlaylistResponse =
        playlistRequest accessToken |> getPlaylistIgnoreCache

    let private getLatestTracks (accessToken: string) =
        let playlist = getPlaylistTracks accessToken
        let offset = playlist.total - 50

        let request = playlistRequest accessToken
        request.RequestUri <- Uri(request.RequestUri.ToString() + $"?offset={offset}&limit=100")

        let response = request |> getPlaylist

        response.items
        |> Array.map (fun item -> $"{item.track.name} | {(item.track.artists |> Array.last).name} | {item.addedAt}")

    let rec private getAllTracksFromSpotify
        (accessToken: string)
        (tracks: Model.PlaylistResponse list)
        : Model.PlaylistResponse list =
        let next = tracks |> List.last |> (_.next)

        if next = null then
            tracks
        else
            let request = playlistRequest accessToken
            request.RequestUri <- Uri(next)
            let nextPlaylist = request |> getPlaylist
            getAllTracksFromSpotify accessToken (tracks @ [ nextPlaylist ])

    let getAllTracks () : Model.PlaylistResponse list =
        let accessToken = Authorization.getAccessToken ()
        let playlist = getPlaylistTracks accessToken
        getAllTracksFromSpotify accessToken [ playlist ]

    let getAllContributors () : string Set =
        getAllTracks ()
        |> List.map (fun playlist -> playlist.items |> List.ofArray)
        |> List.collect id
        |> List.map (_.addedBy.id)
        |> Set.ofList

    let spotifyItemToTrack (item: Model.Item) : Track =
        { id = item.track.id
          name = item.track.name
          artists = item.track.artists |> Array.map (_.name)
          added_by = item.addedBy.id
          added_on = item.addedAt }

    let getAllTracksAsModel () : Track list =
        getAllTracks ()
        |> List.map (fun playlist -> playlist.items |> List.ofArray)
        |> List.collect id
        |> List.map spotifyItemToTrack

    let getLatestTrack () : Model.Item =
        let accessToken = Authorization.getAccessToken ()
        let playlist = getPlaylistTracks accessToken
        let offset = playlist.total - 1

        let request = playlistRequest accessToken
        request.RequestUri <- Uri(request.RequestUri.ToString() + $"?offset={offset}&limit=100")

        let response = request |> getPlaylistIgnoreCache
        response.items |> Array.head

    let getCurrenttotal () : int =
        let accessToken = Authorization.getAccessToken ()
        (getPlaylistTracks accessToken).total

    let getLatestNewTracks (previousTotal: int) : int * Model.PlaylistResponse =
        let accessToken = Authorization.getAccessToken ()
        let playlist = getPlaylistTracks accessToken
        let offset = playlist.total - (playlist.total - previousTotal)

        let request = playlistRequest accessToken
        request.RequestUri <- Uri(request.RequestUri.ToString() + $"?offset={offset}&limit=100")

        let response = request |> getPlaylistIgnoreCache
        playlist.total, response
