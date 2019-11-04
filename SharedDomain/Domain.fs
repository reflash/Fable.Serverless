namespace SharedDomain

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type WeatherInfo = 
  { City : string
    TempInC : float }

  static member Decode (str : string) = Decode.Auto.unsafeFromString<WeatherInfo> str
  static member Encode (wi : WeatherInfo) = Encode.Auto.toString(4, wi)