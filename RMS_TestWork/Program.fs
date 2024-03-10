open System
open System.Net
open System.Net.Http

open Akka.FSharp

type HttpRequestMessageType = 
    | SendRequest of string * HttpMethod
    | CancelRequest

type HttpResponseMessageType = ResponseHandler of HttpResponseMessage

let system = System.create "MainUserActor" <| Configuration.defaultConfig()

let responseHandlerActor =
    spawn system "ResponseHandlerActor"
        <| fun mailbox ->
            let rec loop () = actor {
                let! message = mailbox.Receive()
                match message with
                | ResponseHandler(response) ->
                    if response.StatusCode = HttpStatusCode.OK then
                        Console.WriteLine("Response body: {0}", response.Content.ReadAsStringAsync().Result)
                return! loop()
            }
            loop()

let httpRequestActor =
    spawn system "HttpRequestActor"
        <| fun mailbox ->
            let rec loop () = actor {
                let! message = mailbox.Receive()
                let httpClient = new HttpClient()
                match message with
                | SendRequest(url, method) -> 
                    try
                        let response = httpClient.SendAsync(new HttpRequestMessage(method, url)) |> Async.AwaitTask |> Async.RunSynchronously
                        let responseHandlerActorRef = select "/user/ResponseHandlerActor" system 
                        responseHandlerActorRef <! ResponseHandler(response)
                    with
                    | error -> Console.WriteLine("Error making HTTP request: {0}", error.Message)
                | CancelRequest -> 
                    httpClient.CancelPendingRequests()
                    Console.WriteLine("Request cancelled.")
                return! loop()
            }
            loop()

httpRequestActor <! SendRequest("http://webcode.me", HttpMethod.Get)
httpRequestActor <! CancelRequest

System.Console.ReadLine() |> ignore