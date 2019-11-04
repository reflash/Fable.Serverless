namespace Fable.Serverless

open System.IO
open Newtonsoft.Json
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Logging
open SharedDomain
open Microsoft.Azure.Documents.Client
open Microsoft.Azure.Documents.Linq

module Server =
    let runQuery<'T> (dq:IDocumentQuery<'T>) : 'T list = 
        [
            while (dq.HasMoreResults) 
                do yield!
                    dq.ExecuteNextAsync<'T>()
                    |> Async.AwaitTask          // Task<T>->Async<T>, where T is ResourceResponse<DocumentCollection>
                    |> Async.RunSynchronously
        ]     
    let [<Literal>] MIMEJSON = "application/json"

    let serveStaticContent (log : ILogger) (context : ExecutionContext) (fileName : string)  =
        let filePath = Path.Combine(context.FunctionAppDirectory, "public", fileName) |> Path.GetFullPath
        try
            let file = new FileStream(filePath, FileMode.Open)
            log.LogInformation <| sprintf "File found: %s" filePath
            OkObjectResult file :> ObjectResult
        with _ ->
            let msg = sprintf "File not found: %s" filePath
            log.LogError msg
            BadRequestObjectResult msg :> ObjectResult

    [<FunctionName("serveStatic")>]
    let serveStatic ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "public/{staticFile?}")>] req : HttpRequest,
                     log : ILogger,
                     context : ExecutionContext) =
        log.LogInformation "Serving static content"
        match req.Path with
        | s when s.HasValue && s.Value = "/api/public/" -> "index.html" |> serveStaticContent log context
        | s -> s.Value.Replace("/api/public/", "") |> serveStaticContent log context

    [<FunctionName("serveJSON")>]
    let serveJSON ([<HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "json")>] req : HttpRequest, log : ILogger) =
        log.LogInformation "Serving JSON"
        match req.ContentType with
        | MIMEJSON ->
            let sr = new StreamReader(req.Body)
            let postedModel = sr.ReadToEnd() |> User.Decode
            let msg = sprintf "Hello %s. I see you can count to %i" postedModel.Name postedModel.Count
            let pause = (System.Math.Abs postedModel.Count) * 1000
            System.Threading.Thread.Sleep pause // Sleep to show non-blocking UI
            { postedModel with Message = msg }
            |> User.Encode
            |> OkObjectResult
            :> ObjectResult
        | _ ->
            sprintf "Incorrect mime type. I was expecting json but got: %s" req.ContentType
            |> BadRequestObjectResult
            :> ObjectResult

    [<FunctionName("getCities")>]
    let getCities([<HttpTrigger(
                    AuthorizationLevel.Anonymous, 
                    "get", Route = "getCities")>] req : HttpRequest,
                    [<CosmosDB(
                        "WeatherItems",
                        "Items",
                        ConnectionStringSetting = "CosmosDBConnection")>] client : DocumentClient,
                    log : ILogger) =
               
        log.LogInformation "getCities called"
        let collectionUri = UriFactory.CreateDocumentCollectionUri("WeatherItems", "Items");
        let query = client.CreateDocumentQuery<WeatherInfo>(collectionUri)
                            .AsDocumentQuery()
        let results = runQuery query
        results
        |> JsonConvert.SerializeObject
        |> OkObjectResult
        :> ObjectResult

        