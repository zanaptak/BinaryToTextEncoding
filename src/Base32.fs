namespace Zanaptak.BinaryToTextEncoding
open Utils

module private Base32Utils =
    open System

    // Increase by 8/5 ratio
    let inline byteCountToCharCount ( byteCount : int ) =
        let count = float byteCount * 1.6 |> ceil
        if count <= Int32MaxAsFloat then int count else raiseFormatException "byte count too large"

    // Decrease by 5/8 ratio
    let inline charCountToByteCount ( charCount : int ) =
        float charCount * 0.625 |> floor |> int

    let encodeInternal ( configuration : BinaryToTextConfiguration ) wrapAtCol ( bytes : byte array ) =
        if isNull bytes then raise ( ArgumentNullException( "bytes" ) )

        let totalChars = byteCountToCharCount bytes.Length

        if totalChars > 0 then

            let wrapAtCol = ( max 0 wrapAtCol ) &&& ~~~3 // restrict to multiple of 4
            let paddingChars =
                if configuration.PadOnEncode then
                    match totalChars &&& 0b111 with
                    | 0 -> 0
                    | x -> 8 - x
                else 0
            let lineFeedChars =
                if wrapAtCol = 0 then 0
                else ( totalChars + paddingChars - 1 ) / wrapAtCol * configuration.Newline.Length
            let outChars : char array = Array.zeroCreate ( totalChars + paddingChars + lineFeedChars )

            let mutable charIndex = 0
            let mutable colIndex = 0
            let mutable bitBuffer = 0
            // This will be 0 or greater when 20 bits or more are in buffer.
            // Used for top level comparison in loop; if 0 or greater then encode 4 chars.
            // Offset used instead of bitcount to avoid extra subtraction operation when calculating bit shifts.
            let mutable fullChunkBitOffset = -20

            let inline wrapIfNeeded() =
                if wrapAtCol > 0 && colIndex >= wrapAtCol then
                    Array.Copy( configuration.Newline , 0 , outChars , charIndex , configuration.Newline.Length )
                    charIndex <- charIndex + configuration.Newline.Length
                    colIndex <- 0

            // Add each byte on right side of buffer, shifting previous bits left.
            // Encode leftmost bits of buffer when enough exist for a character chunk.
            for byte in bytes do
                bitBuffer <- bitBuffer <<< 8 ||| int byte
                fullChunkBitOffset <- fullChunkBitOffset + 8
                // Encode 20-bit chunk into 4 chars
                if fullChunkBitOffset >= 0 then
                    wrapIfNeeded()
                    let bitValue = bitBuffer >>> fullChunkBitOffset
                    outChars.[ charIndex ] <- configuration.ValueToChar.[ bitValue >>> 15 &&& 0b1_1111 ]
                    outChars.[ charIndex + 1 ] <- configuration.ValueToChar.[ bitValue >>> 10 &&& 0b1_1111 ]
                    outChars.[ charIndex + 2 ] <- configuration.ValueToChar.[ bitValue >>> 5 &&& 0b1_1111 ]
                    outChars.[ charIndex + 3 ] <- configuration.ValueToChar.[ bitValue &&& 0b1_1111 ]
                    charIndex <- charIndex + 4
                    colIndex <- colIndex + 4
                    fullChunkBitOffset <- fullChunkBitOffset - 20

            // Encode any remaining 5-bit chunks
            while fullChunkBitOffset >= -15 do
                wrapIfNeeded()
                let bitValue = bitBuffer >>> ( fullChunkBitOffset + 15 )
                outChars.[ charIndex ] <- configuration.ValueToChar.[ bitValue &&& 0b1_1111 ]
                charIndex <- charIndex + 1
                colIndex <- colIndex + 1
                fullChunkBitOffset <- fullChunkBitOffset - 5

            // Encode final partial chunk if necessary
            if fullChunkBitOffset > -20 then
                wrapIfNeeded()
                let bitValue = bitBuffer <<< ( 5 - ( fullChunkBitOffset + 20 ) ) // shift left to encode as big endian
                outChars.[ charIndex ] <- configuration.ValueToChar.[ bitValue &&& 0b1_1111 ]
                charIndex <- charIndex + 1
                colIndex <- colIndex + 1
                fullChunkBitOffset <- fullChunkBitOffset - 5

            if paddingChars > 0 then
                let paddingCharacter = configuration.PaddingCharacter |> Option.defaultValue ' '
                for _ in 1 .. paddingChars do
                    wrapIfNeeded()
                    outChars.[ charIndex ] <- paddingCharacter
                    charIndex <- charIndex + 1
                    colIndex <- colIndex + 1

            if charIndex = outChars.Length then
                String outChars
            else
                String( outChars , 0 , charIndex )

        else ""

    let decodeInternal ( configuration : BinaryToTextConfiguration ) ( str : string ) =
        if isNull str then raise ( ArgumentNullException( "str" ) )

        let outBytes : byte array = Array.zeroCreate ( charCountToByteCount str.Length )

        let mutable byteIndex = 0
        let mutable bitBuffer = 0
        let mutable fullChunkBitOffset = -24
        let mutable paddingFound = false

        // Decode each character as 5 bits into buffer; copy from buffer to output in 24-bit chunks
        for currChar in str do
            let currCharCode = int currChar
            if isCharInsideRange currCharCode then
                let decodeVal = configuration.CharCodeToValue.[ currCharCode ]
                if decodeVal >= 0 && not paddingFound then
                    // Decode 1 char chunk into 5 bits
                    bitBuffer <- bitBuffer <<< 5 ||| decodeVal
                    fullChunkBitOffset <- fullChunkBitOffset + 5
                    // Copy 24 bits into 3 bytes
                    if fullChunkBitOffset >= 0 then
                        let bitValue = bitBuffer >>> fullChunkBitOffset
                        outBytes.[ byteIndex ] <- bitValue >>> 16 |> byte
                        outBytes.[ byteIndex + 1 ] <- bitValue >>> 8 |> byte
                        outBytes.[ byteIndex + 2 ] <- bitValue |> byte
                        byteIndex <- byteIndex + 3
                        fullChunkBitOffset <- fullChunkBitOffset - 24
                elif decodeVal = PaddingChar then paddingFound <- true
                elif decodeVal >= 0 && paddingFound then
                    raiseFormatException "invalid input: character '%c' found after padding" currChar
                else raiseFormatException "invalid input: invalid character '%c'" currChar
            elif not ( isCharWhitespace currCharCode ) then raiseFormatException "invalid input: invalid character code 0x%x" currCharCode

        // Copy any remaining 8-bit chunks to output
        while fullChunkBitOffset >= -16 do
            let bitValue = bitBuffer >>> ( fullChunkBitOffset + 16 )
            outBytes.[ byteIndex ] <- bitValue |> byte
            byteIndex <- byteIndex + 1
            fullChunkBitOffset <- fullChunkBitOffset - 8

        // Sanity check leftover bits.
        // Raise error if leftover bits are invalid; we don't want multiple strings mapping to the same binary value.
        // Byte bits [1.....][2.....][3.....][4.....][5.....]
        // Char bits [1..][2..][3..][4..][5..][6..][7..][8..]
        // Every 8 chars encodes 5 bytes:
        //  1 char = 5 bits = 0 bytes, 5 bits leftover (unused extra char)
        //  2 chars = 10 bits = 1 bytes, 2 bits leftover
        //  3 chars = 15 bits = 1 bytes, 7 bits leftover (unused extra char)
        //  4 chars = 20 bits = 2 bytes, 4 bits leftover
        //  5 chars = 25 bits = 3 bytes, 1 bit leftover
        //  6 chars = 30 bits = 3 bytes, 6 bits leftover (unused extra char)
        //  7 chars = 35 bits = 4 bytes, 3 bits leftover
        //  8 chars = 40 bits = 5 bytes, 0 bits leftover
        if fullChunkBitOffset > -24 then
            let leftoverBitCount = fullChunkBitOffset + 24
            if leftoverBitCount >= 5 then raiseFormatException "invalid input: unused final character"
            let leftoverBitValue = bitBuffer <<< ( 8 - leftoverBitCount ) &&& 0b1111_1111
            if leftoverBitValue > 0 then raiseFormatException "invalid input: extra non-zero bits" // overflow bits should all be zero

        if byteIndex = outBytes.Length then
            outBytes
        else
            Array.truncate byteIndex outBytes

    let [< Literal >] defaultCharacterSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"

open Base32Utils
open System.Runtime.InteropServices

type Base32 private ( configuration : BinaryToTextConfiguration ) =

    static let defaultInstance = lazy Base32( defaultCharacterSet , padding = "=" )

    /// Encodes a byte array into a Base32 string. Optionally wrap output at specified column (will be rounded down to a multiple of 4 for implementation efficiency). Throws exception on invalid input.
    member this.Encode ( bytes : byte array , [< Optional ; DefaultParameterValue( defaultWrapAtColumn ) >] wrapAtColumn : int ) =
        encodeInternal configuration wrapAtColumn bytes
    /// Decodes a Base32 string into a byte array. Throws exception on invalid input.
    member this.Decode ( str : string ) = decodeInternal configuration str
    /// Returns a configuration object describing the character set and newline setting used by this instance.
    member this.Configuration = configuration

    /// Provides a static Base32 encoder/decoder instance using the default options. 8 characters = 5 bytes; 1 character = 5 bits.
    static member Default = defaultInstance.Value

    /// (Default) RFC 4648 section 6: ABCDEFGHIJKLMNOPQRSTUVWXYZ234567
    static member StandardCharacterSet = defaultCharacterSet

    /// RFC 4648 section 7, ASCII-sortable: 0123456789ABCDEFGHIJKLMNOPQRSTUV
    static member HexExtendedCharacterSet = "0123456789ABCDEFGHIJKLMNOPQRSTUV"

    /// Excludes numbers, vowels, and some confusable letters, ASCII-sortable: BCDFHJKMNPQRSTXZbcdfhjkmnpqrstxz
    static member ConsonantsCharacterSet = "BCDFHJKMNPQRSTXZbcdfhjkmnpqrstxz"

    /// <summary>Creates a Base32 encoder/decoder using the specified options. 8 characters = 5 bytes; 1 character = 5 bits.</summary>
    /// <param name='characterSet'>A 32-character string. Characters must be in the range U+0021 to U+007E. Default: ABCDEFGHIJKLMNOPQRSTUVWXYZ234567</param>
    /// <param name='useCrLfNewline'>Specifies whether to use CRLF (true) or LF (false) when encoding with the wrap option. Default: true</param>
    /// <param name='forceCaseSensitive'>Specifies whether to require letters to match their case in the character set when decoding, even when the character set has no repeat letters and could otherwise accept either case. Default: false</param>
    /// <param name='padding'>String containing a trailing padding character to ignore when decoding. Must be a single character in the range U+0021 to U+007E and not contained in the character set, or enpty/null for no padding support. Default: empty</param>
    /// <param name='padOnEncode'>Specifies whether to add trailing padding when encoding. The 'padding' parameter must also be specified. Default: false</param>
    new
        (
            [< Optional ; DefaultParameterValue( defaultCharacterSet ) >] characterSet : string
            , [< Optional ; DefaultParameterValue( defaultUseCrLfNewline ) >] useCrLfNewline : bool
            , [< Optional ; DefaultParameterValue( defaultForceCaseSensitive ) >] forceCaseSensitive : bool
            , [< Optional ; DefaultParameterValue( defaultPadding ) >] padding : string
            , [< Optional ; DefaultParameterValue( defaultPadOnEncode ) >] padOnEncode : bool
        ) =
            Base32( BinaryToTextConfiguration ( 32 , characterSet , useCrLfNewline , forceCaseSensitive , padding , padOnEncode ) )
