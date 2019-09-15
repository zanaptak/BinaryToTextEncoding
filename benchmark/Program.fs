open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open Zanaptak.BinaryToTextEncoding
open System

type EncodeBenchmark() =
  let r = System.Random( 1234567 )
  let bytes : byte array = Array.zeroCreate 16
  do r.NextBytes bytes

  [<Benchmark>]
  member this.NetBase64Encode () =
    Convert.ToBase64String bytes

  [<Benchmark>]
  member this.ZanaptakBase16Encode () =
    Base16.Default.Encode bytes

  [<Benchmark>]
  member this.ZanaptakBase32Encode () =
    Base32.Default.Encode bytes

  [<Benchmark>]
  member this.ZanaptakBase46Encode () =
    Base46.Default.Encode bytes

  [<Benchmark>]
  member this.ZanaptakBase64Encode () =
    Base64.Default.Encode bytes

  [<Benchmark>]
  member this.ZanaptakBase91Encode () =
    Base91.Default.Encode bytes

  [<Benchmark>]
  member this.ZanaptakBase91LegacyEncode () =
    Base91Legacy.Default.Encode bytes

type DecodeBenchmark() =
  let r = System.Random( 1234567 )
  let bytes : byte array = Array.zeroCreate 16
  do r.NextBytes bytes
  let netBase64Str = Convert.ToBase64String bytes
  let base16Str = Base16.Default.Encode bytes
  let base32Str = Base32.Default.Encode bytes
  let base46Str = Base46.Default.Encode bytes
  let base64Str = Base64.Default.Encode bytes
  let base91Str = Base91.Default.Encode bytes
  let base91LegacyStr = Base91Legacy.Default.Encode bytes

  [<Benchmark>]
  member this.NetBase64Decode () =
    Convert.FromBase64String netBase64Str

  [<Benchmark>]
  member this.ZanaptakBase16Decode () =
    Base16.Default.Decode base16Str

  [<Benchmark>]
  member this.ZanaptakBase32Decode () =
    Base32.Default.Decode base32Str

  [<Benchmark>]
  member this.ZanaptakBase46Decode () =
    Base46.Default.Decode base46Str

  [<Benchmark>]
  member this.ZanaptakBase64Decode () =
    Base64.Default.Decode base64Str

  [<Benchmark>]
  member this.ZanaptakBase91Decode () =
    Base91.Default.Decode base91Str

  [<Benchmark>]
  member this.ZanaptakBase91LegacyDecode () =
    Base91Legacy.Default.Decode base91LegacyStr

type Encode999Benchmark() =
  let r = System.Random( 1234567 )
  let bytes : byte array = Array.zeroCreate 999
  do r.NextBytes bytes

  [<Benchmark>]
  member this.NetBase64Encode () =
    Convert.ToBase64String bytes

  [<Benchmark>]
  member this.ZanaptakBase16Encode () =
    Base16.Default.Encode bytes

  [<Benchmark>]
  member this.ZanaptakBase32Encode () =
    Base32.Default.Encode bytes

  [<Benchmark>]
  member this.ZanaptakBase46Encode () =
    Base46.Default.Encode bytes

  [<Benchmark>]
  member this.ZanaptakBase64Encode () =
    Base64.Default.Encode bytes

  [<Benchmark>]
  member this.ZanaptakBase91Encode () =
    Base91.Default.Encode bytes

  [<Benchmark>]
  member this.ZanaptakBase91LegacyEncode () =
    Base91Legacy.Default.Encode bytes

type Decode999Benchmark() =
  let r = System.Random( 1234567 )
  let bytes : byte array = Array.zeroCreate 999
  do r.NextBytes bytes
  let netBase64Str = Convert.ToBase64String bytes
  let base16Str = Base16.Default.Encode bytes
  let base32Str = Base32.Default.Encode bytes
  let base46Str = Base46.Default.Encode bytes
  let base64Str = Base64.Default.Encode bytes
  let base91Str = Base91.Default.Encode bytes
  let base91LegacyStr = Base91Legacy.Default.Encode bytes

  [<Benchmark>]
  member this.NetBase64Decode () =
    Convert.FromBase64String netBase64Str

  [<Benchmark>]
  member this.ZanaptakBase16Decode () =
    Base16.Default.Decode base16Str

  [<Benchmark>]
  member this.ZanaptakBase32Decode () =
    Base32.Default.Decode base32Str

  [<Benchmark>]
  member this.ZanaptakBase46Decode () =
    Base46.Default.Decode base46Str

  [<Benchmark>]
  member this.ZanaptakBase64Decode () =
    Base64.Default.Decode base64Str

  [<Benchmark>]
  member this.ZanaptakBase91Decode () =
    Base91.Default.Decode base91Str

  [<Benchmark>]
  member this.ZanaptakBase91LegacyDecode () =
    Base91Legacy.Default.Decode base91LegacyStr

[<EntryPoint>]
let main argv =
  BenchmarkSwitcher
    .FromTypes( [| typeof< EncodeBenchmark > ; typeof< DecodeBenchmark > ; typeof< Encode999Benchmark > ; typeof< Decode999Benchmark > |] )
    .RunAll()
    |> ignore
  0
