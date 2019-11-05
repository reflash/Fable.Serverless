namespace Fable.Serverless

open System
open System.IO
open System.Collections.Generic
open Newtonsoft.Json
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Logging
open SharedDomain
open Microsoft.Azure.Documents.Client
open Microsoft.Azure.Documents.Linq
open Hopac
open HttpFs.Client

module Server =
    let runQuery<'T> (dq:IDocumentQuery<'T>) : 'T list = 
        [
            while (dq.HasMoreResults) 
                do yield!
                    dq.ExecuteNextAsync<'T>()
                    |> Async.AwaitTask          // Task<T>->Async<T>, where T is ResourceResponse<DocumentCollection>
                    |> Async.RunSynchronously
        ]
    
    let request url =
        Request.createUrl Get url
        |> Request.setHeader (Accept "application/json")
        |> Request.responseAsString
        |> run

    let basePath = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")
    let [<Literal>] CosmosDbConnectionName = "CosmosDBConnection"
    let [<Literal>] WeatherDb = "WeatherItems"
    let [<Literal>] WeatherCollection = "Items"
    let [<Literal>] WeatherSql = "select * from WeatherItems r where r.city = {city}"

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

    [<FunctionName("serveWebsite")>]
    let serveStatic ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{staticFile?}")>] req : HttpRequest,
                     log : ILogger,
                     context : ExecutionContext) =
        log.LogInformation "Serving website content"
        match req.Path with
        | s when s.Value = "/api/" -> "index.html" |> serveStaticContent log context
        | s -> s.Value.Replace("/api/", "") |> serveStaticContent log context

    [<FunctionName("getCities")>]
    let getCities([<HttpTrigger(
                    AuthorizationLevel.Anonymous, 
                    "get", Route = "getCities")>] req : HttpRequest,
                    [<CosmosDB(WeatherDb, WeatherCollection, 
                      ConnectionStringSetting = CosmosDbConnectionName)>] client : DocumentClient,
                    log : ILogger) =
               
        log.LogInformation "getCities called"
        let collectionUri = UriFactory.CreateDocumentCollectionUri(WeatherDb, "Items");
        let query = client.CreateDocumentQuery<WeatherInfo>(collectionUri)
                            .AsDocumentQuery()
        let results = runQuery query
        results
        |> List.map (fun wi -> wi.City)
        |> JsonConvert.SerializeObject
        |> OkObjectResult
        :> ObjectResult

    // scale is either C or F
    [<FunctionName("getTemp")>]
    let getTemp([<HttpTrigger(
                    AuthorizationLevel.Anonymous, 
                    "get", Route = "getTemp/{city}/{scale}")>] req : HttpRequest,
                    city: string,
                    scale: string,
                    log : ILogger) =
               
        log.LogInformation ("getTemp for {0} temp of {1} called", city, scale)
        match scale.ToUpper() with
        | "C" -> 
            sprintf "http://%s/api/getTempC/%s" basePath city
            |> request
            |> OkObjectResult 
            :> ObjectResult
        | "F" -> 
            sprintf "http://%s/api/getTempF/%s" basePath city
            |> request
            |> OkObjectResult 
            :> ObjectResult
        | _ -> failwith "wrong scale"

    // (0°C × 9/5) + 32 = 32°F
    [<FunctionName("getTempF")>]
    let getTempF([<HttpTrigger(
                    AuthorizationLevel.Anonymous, 
                    "get", Route = "getTempF/{city}")>] req : HttpRequest,
                    city: string,
                    log : ILogger) =
               
        log.LogInformation ("getTempF for {0} called", city)
        sprintf "http://%s/api/getTempC/%s" basePath city
        |> request
        |> float |> fun c -> (c * 9. / 5.) + 32.
        |> OkObjectResult
        :> ObjectResult 
    
    [<FunctionName("getTempC")>]
    let getTempC([<HttpTrigger(
                    AuthorizationLevel.Anonymous, 
                    "get", Route = "getTempC/{city}")>] req : HttpRequest,
                    [<CosmosDB(WeatherDb, WeatherCollection, ConnectionStringSetting = CosmosDbConnectionName,
                        SqlQuery = WeatherSql)>] wis: IEnumerable<WeatherInfo>,
                    log : ILogger) =
        let wi = Seq.head wis
        log.LogInformation ("getTempC for {0} called", wi.City)
        wi.TempInC
        |> OkObjectResult
        :> ObjectResult