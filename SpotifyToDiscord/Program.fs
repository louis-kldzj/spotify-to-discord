open System
open SpotifyToWhatsApp


let rec mainLoop () =
    printf "sptapp:>"
    Console.ReadLine() |> Commands.argumentToCommand |> Commands.commandToAction
    mainLoop ()

let rec mainLoopDebug () =
    printf "sptapp-dbg:>"

    Console.ReadLine()
    |> Commands.argumentToCommand
    |> Commands.commandToActionDebug

    mainLoopDebug ()

let test () =
    "send-test-track" |> Commands.argumentToCommand |> Commands.commandToActionDebug

[<EntryPoint>]
let main args =

    printfn "Welcome to the Spotify to WhatsApp App"
    mainLoopDebug ()

    0
