namespace Zanaptak.BinaryToTextEncoding
open Utils

module private Base16Utils =
    open System

    let encodeInternal ( configuration : BinaryToTextConfiguration ) wrapAtCol ( bytes : byte array ) =
        if isNull bytes then raise ( ArgumentNullException( "bytes" ) )

        let wrapAtCol = ( max 0 wrapAtCol ) &&& ~~~3 // restrict to multiple of 4
        let totalChars = bytes.Length * 2

        if totalChars > 0 then
            let lineFeedChars =
                if wrapAtCol = 0 then 0
                else ( totalChars - 1 ) / wrapAtCol * configuration.Newline.Length
            let outChars : char array = Array.zeroCreate ( totalChars + lineFeedChars )

            let mutable charIndex = 0
            let mutable colIndex = 0
            // Encode each byte into 2 chars
            for byte in bytes do
                if wrapAtCol > 0 && colIndex >= wrapAtCol then
                    Array.Copy( configuration.Newline , 0 , outChars , charIndex , configuration.Newline.Length )
                    charIndex <- charIndex + configuration.Newline.Length
                    colIndex <- 0
                let byteIntValue = int byte
                outChars.[ charIndex ] <- configuration.ValueToChar.[ byteIntValue >>> 4 &&& 0xF ]
                outChars.[ charIndex + 1 ] <- configuration.ValueToChar.[ byteIntValue &&& 0xF ]
                charIndex <- charIndex + 2
                colIndex <- colIndex + 2

            String outChars

        else ""

    let decodeInternal ( configuration : BinaryToTextConfiguration ) ( str : string ) =
        if isNull str then raise ( ArgumentNullException( "str" ) )
        let inputLength = usableLength str

        let outBytes : byte array = Array.zeroCreate ( inputLength / 2 )
        let mutable byteIndex = 0
        let mutable prevCharCode = 0

        for currChar in str do
            let currCharCode = int currChar
            if isCharInsideRange currCharCode then
                if prevCharCode = 0 then
                    prevCharCode <- currCharCode
                else
                    // Decode 2 char chunk into 1 byte
                    let hiDecode , loDecode = configuration.CharCodeToValue.[ prevCharCode ] , configuration.CharCodeToValue.[ currCharCode ]
                    if hiDecode <> InvalidDecode && loDecode <> InvalidDecode then
                        outBytes.[ byteIndex ] <- hiDecode <<< 4 ||| loDecode |> byte
                        byteIndex <- byteIndex + 1
                        prevCharCode <- 0
                    elif hiDecode = InvalidDecode then raiseFormatException "invalid input: invalid char '%c'" ( char prevCharCode )
                    elif loDecode = InvalidDecode then raiseFormatException "invalid input: invalid char '%c'" ( char currCharCode )
            elif not ( isCharWhitespace currCharCode ) then raiseFormatException "invalid input: invalid char code 0x%x" currCharCode

        if prevCharCode <> 0 then raiseFormatException "invalid input: extra char '%c'" ( char prevCharCode )

        outBytes

    let [< Literal >] defaultCharacterSet = "0123456789ABCDEF"

open Base16Utils
open System.Runtime.InteropServices

type Base16 private ( configuration : BinaryToTextConfiguration ) =

    static let defaultInstance = lazy Base16( defaultCharacterSet )

    /// Encodes a byte array into a Base16 string. Optionally wrap output at specified column (will be rounded down to a multiple of 4 for implementation efficiency). Throws exception on invalid input.
    member this.Encode ( bytes : byte array , [< Optional ; DefaultParameterValue( defaultWrapAtColumn ) >] wrapAtColumn : int ) =
        encodeInternal configuration wrapAtColumn bytes
    /// Decodes a Base16 string into a byte array. Throws exception on invalid input.
    member this.Decode ( str : string ) = decodeInternal configuration str
    /// Returns a configuration object describing the character set and newline setting used by this instance.
    member this.Configuration = configuration

    /// Provides a static Base16 encoder/decoder instance using the default options. 2 characters = 1 byte; 1 character = 4 bits.
    static member Default = defaultInstance.Value

    /// (Default) Standard hexadecimal notation, ASCII-sortable: 0123456789ABCDEF
    static member StandardCharacterSet = defaultCharacterSet

    /// Excludes numbers, vowels, and some confusable letters, ASCII-sortable: BCDFHJKMNPQRSTXZ
    static member ConsonantsCharacterSet = "BCDFHJKMNPQRSTXZ"

    /// <summary>Creates a Base16 encoder/decoder using the specified options. 2 characters = 1 byte; 1 character = 4 bits.</summary>
    /// <param name='characterSet'>A 16-character string. Characters must be in the range U+0021 to U+007E. Default: 0123456789ABCDEF</param>
    /// <param name='useCrLfNewline'>Specifies whether to use CRLF (true) or LF (false) when encoding with the wrap option. Default: true</param>
    new
        (
            [< Optional ; DefaultParameterValue( defaultCharacterSet ) >] characterSet : string
            , [< Optional ; DefaultParameterValue( defaultUseCrLfNewline ) >] useCrLfNewline : bool
            , [< Optional ; DefaultParameterValue( defaultForceCaseSensitive ) >] forceCaseSensitive : bool
        ) =
            Base16( BinaryToTextConfiguration ( 16 , characterSet , useCrLfNewline , forceCaseSensitive ) )
