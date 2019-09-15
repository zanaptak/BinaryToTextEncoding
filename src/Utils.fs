namespace Zanaptak.BinaryToTextEncoding
open System

module internal Utils =

  let raiseFormatException fmt =
    fmt |> Printf.kprintf ( fun s -> raise ( FormatException( s ) ) )

  // Length excluding whitespace, for decoding routines to determine correct buffer size.
  // Actually excludes all low value control characters - these will throw exception later
  // anyway so no need to perform extra comparisons to distinguish them now.
  let usableLength ( str : string ) =
    let mutable length = str.Length
    for currChar in str do
      if currChar <= ' ' then length <- length - 1
    length

  let inline isCharInsideRange charCode =
    charCode >= 0x21 && charCode <= 0x7E

  let inline isCharWhitespace charCode =
    charCode = 0xA || charCode = 0xD || charCode = 0x9 || charCode = 0x20

  let [< Literal >] InvalidDecode = -1
  let [< Literal >] Int32MaxAsFloat = 2147483647.

open Utils

type BinaryToTextConfiguration internal ( charCount , characterSet : string , useCrLfNewline ) =
  do if String.IsNullOrWhiteSpace characterSet then raise ( System.ArgumentNullException( "characterSet" ) )

  let valueToChar =
    characterSet
    |> Seq.filter ( fun c -> isCharInsideRange ( int c ) )
    |> Seq.distinct
    |> Seq.toArray

  do
    if valueToChar.Length <> charCount then
      let message = sprintf "character set must be %i distinct chars in the range U+0021 to U+007E" charCount
      raise ( System.ArgumentException( message , "characterSet" ) )

  let characterSet = String valueToChar
  let charCodeToValue = Array.replicate 0x7F InvalidDecode

  do valueToChar |> Array.iteri ( fun i c -> charCodeToValue.[ int c ] <- i )

  let newline = if useCrLfNewline then [| '\r' ; '\n' |] else [| '\n' |]

  member internal this.ValueToChar = valueToChar
  member internal this.CharCodeToValue = charCodeToValue
  member internal this.Newline = newline
  member this.CharacterSet = characterSet
  member this.UseCrLfNewline = useCrLfNewline
