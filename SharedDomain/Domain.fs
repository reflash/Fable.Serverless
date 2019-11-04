namespace SharedDomain

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type User =
    { Name : string
      Count : int
      Message : string }
    static member Decode (str : string) = Decode.Auto.unsafeFromString<User> str
    static member Encode (user : User) = Encode.Auto.toString(4, user)

type WeatherInfo = 
  { City : string
    TempInC : int }

  static member Decode (str : string) = Decode.Auto.unsafeFromString<WeatherInfo> str
  static member Encode (wi : WeatherInfo) = Encode.Auto.toString(4, wi)