module SpotifyToWhatsApp.Discord

open System
open System.Collections.Generic
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json

module Model =

    type TrackAdded =
        { by: string
          playlistUrl: string
          trackName: string
          trackUrl: string
          artists: string }

    let toTrackAdded (spotifyItem: Spotify.Model.Item) : TrackAdded =
        let addedBy = Data.DataBase.getContributorName spotifyItem.addedBy.id

        { by = addedBy
          playlistUrl = Environment.GetEnvironmentVariable("ENV.THEREAL.SPOTIFY.PLAYLIST_URL")
          trackName = spotifyItem.track.name
          trackUrl = spotifyItem.track.externalUrls.spotify
          artists = spotifyItem.track.artists |> Array.map (_.name) |> String.concat ", " }


open Model

module Webhook =
    let private _url =
        Environment.GetEnvironmentVariable("ENV.THEREAL.DISCORD.WEBHOOK_URL")

    let private createRequestMessage () : HttpRequestMessage =
        new HttpRequestMessage(HttpMethod.Post, _url)

    let private songAddedMessage (track: TrackAdded) : string =
        $"{track.by} just added a song to the [Everything]({track.playlistUrl}) playlist!\n\n[{track.trackName}]({track.trackUrl}) **- {track.artists}**"

    let private createMessageMap (message: string) : Dictionary<string, obj> =
        let dict = Dictionary<string, obj>()
        dict.Add("content", message)
        dict.Add("embeds", null)
        dict.Add("attachments", [])
        dict

    let private createRequestMessageWithMessage (message: string) : HttpRequestMessage =
        let requestMessage = createRequestMessage ()
        let map = createMessageMap message
        let content = JsonContent.Create<Dictionary<string, obj>>(map)
        requestMessage.Content <- content
        requestMessage

    let sendTestMessage () =
        let request = createRequestMessageWithMessage "I'm a test!"
        let response = Net.sendRequest request |> Async.RunSynchronously
        let content = response.Content.ReadAsStringAsync().Result
        printfn $"Content: {content}"
        ()

    let sendSongAddedMessage (trackItem: Spotify.Model.Item) =
        let message = songAddedMessage (toTrackAdded trackItem)
        let request = createRequestMessageWithMessage message
        let response = Net.sendRequest request |> Async.RunSynchronously
        let content = response.Content.ReadAsStringAsync().Result
        printfn $"Content: {content}"
        ()
