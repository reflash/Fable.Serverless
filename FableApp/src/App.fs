module FableApp

open System
open Elmish
open Elmish.React
open Fable.React
open Fable.React.Props
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Thoth
open Thoth.Fetch
open SharedDomain

// Currently need to set the URL manually for local testing etc
type Model =
    { Cities : string list
      SelectedCity: string
      Temp : float }
    member this.UpdateCities cities = { this with Cities = cities }
    member this.SelectCity city = { this with SelectedCity = city }
    member this.UpdateTemp temp = { this with Temp = temp }
    member this.GetCitiesUrl ()  =
        "/api/getCities"
    member this.GetTempUrl ()  =
        sprintf "/api/getTemp/%s/F" this.SelectedCity

type Msg =
    | Submit
    | FetchCities
    | FetchedCities of string list
    | ChooseCity of string
    | FetchedTemp of float

let init() : Model * Cmd<Msg> =
    { Cities = [] ; SelectedCity = ""; Temp = 0. }, Cmd.ofMsg FetchCities

let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
    match msg with
    | FetchCities ->
        let msg : JS.Promise<Msg> =
            promise {
                let! cities = Fetch.get(model.GetCitiesUrl())
                return (FetchedCities cities)
            }
        model , Cmd.OfPromise.result msg
    | FetchedCities cities -> model.UpdateCities(cities), Cmd.none
    | FetchedTemp temp -> model.UpdateTemp(temp), Cmd.none
    | ChooseCity city -> model.SelectCity(city), Cmd.none
    | Submit ->
        let msg : JS.Promise<Msg> =
            promise {
                let! temp = Fetch.get(model.GetTempUrl())
                return (FetchedTemp temp)
            }
        model , Cmd.OfPromise.result msg

// Rendered with Preact
let view (model : Model) dispatch =
  let options = (List.map (fun city -> option [Value city] [str city]) model.Cities)
  div []
      [ div []
            [ str "Cities:"; 
                select [
                    Value model.SelectedCity
                    OnChange (fun ev ->
                        ChooseCity ev.target?value
                        |> dispatch
                    )
                ] options
            ]
        p [] [ sprintf "Selected city: %s" model.SelectedCity |> str ]
        p [] [ sprintf "Current temp in %s is %f" model.SelectedCity model.Temp |> str ]

        button [ OnClick (fun _ -> dispatch Submit) ] [ str "Send" ]
      ]

Program.mkProgram init update view
|> Program.withReactSynchronous "elmish-app"
|> Program.withConsoleTrace
|> Program.run
