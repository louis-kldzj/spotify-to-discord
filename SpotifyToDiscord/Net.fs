module SpotifyToWhatsApp.Net

open System.Net.Http

let sendRequest (request: HttpRequestMessage) : Async<HttpResponseMessage> =
    let client = new HttpClient()
    printfn $"Making request to {request.RequestUri}"

    async {
        let! response = client.SendAsync(request) |> Async.AwaitTask
        printfn $"Got {response.StatusCode}"
        return response
    }
