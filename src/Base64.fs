namespace Zanaptak.BinaryToTextEncoding
open Utils

module private Base64Utils =
    open System

    // Increase by 4/3 ratio
    let inline byteCountToCharCount ( byteCount : int ) =
        let count = float byteCount * 1.33333333333333 |> ceil
        if count <= Int32MaxAsFloat then int count else raiseFormatException "byte count too large"

    // Decrease by 3/4 ratio
    let inline charCountToByteCount ( charCount : int ) =
        float charCount * 0.75 |> floor |> int

    let encodeInternal ( configuration : BinaryToTextConfiguration ) wrapAtCol ( bytes : byte array ) =
        if isNull bytes then raise ( ArgumentNullException( "bytes" ) )

        let totalChars = byteCountToCharCount bytes.Length

        if totalChars > 0 then

            let wrapAtCol = ( max 0 wrapAtCol ) &&& ~~~3 // restrict to multiple of 4
            let paddingChars =
                if configuration.PadOnEncode then
                    match totalChars &&& 0b11 with
                    | 0 -> 0
                    | x -> 4 - x
                else 0
            let lineFeedChars =
                if wrapAtCol = 0 then 0
                else ( totalChars + paddingChars - 1 ) / wrapAtCol * configuration.Newline.Length
            let outChars : char array = Array.zeroCreate ( totalChars + paddingChars + lineFeedChars )

            let mutable charIndex = 0
            let mutable colIndex = 0
            let mutable bitBuffer = 0
            // This will be 0 or greater when 24 bits or more are in buffer.
            // Used for top level comparison in loop; if 0 or greater then encode 4 chars.
            // Offset used instead of bitcount to avoid extra subtraction operation when calculating bit shifts.
            let mutable fullChunkBitOffset = -24

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
                // Encode 24-bit chunk into 4 chars
                if fullChunkBitOffset >= 0 then
                    wrapIfNeeded()
                    let bitValue = bitBuffer >>> fullChunkBitOffset
                    outChars.[ charIndex ] <- configuration.ValueToChar.[ bitValue >>> 18 &&& 0b11_1111 ]
                    outChars.[ charIndex + 1 ] <- configuration.ValueToChar.[ bitValue >>> 12 &&& 0b11_1111 ]
                    outChars.[ charIndex + 2 ] <- configuration.ValueToChar.[ bitValue >>> 6 &&& 0b11_1111 ]
                    outChars.[ charIndex + 3 ] <- configuration.ValueToChar.[ bitValue &&& 0b11_1111 ]
                    charIndex <- charIndex + 4
                    colIndex <- colIndex + 4
                    fullChunkBitOffset <- fullChunkBitOffset - 24

            // Encode any remaining 6-bit chunks
            while fullChunkBitOffset >= -18 do
                wrapIfNeeded()
                let bitValue = bitBuffer >>> ( fullChunkBitOffset + 18 )
                outChars.[ charIndex ] <- configuration.ValueToChar.[ bitValue &&& 0b11_1111 ]
                charIndex <- charIndex + 1
                colIndex <- colIndex + 1
                fullChunkBitOffset <- fullChunkBitOffset - 6

            // Encode final partial chunk if necessary
            if fullChunkBitOffset > -24 then
                wrapIfNeeded()
                let remainingBitCount = fullChunkBitOffset + 24
                let bitValue = bitBuffer <<< ( 6 - remainingBitCount ) // shift left to encode as big endian
                outChars.[ charIndex ] <- configuration.ValueToChar.[ bitValue &&& 0b11_1111 ]
                charIndex <- charIndex + 1
                colIndex <- colIndex + 1
                fullChunkBitOffset <- fullChunkBitOffset - 6

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

        // Decode each character as 6 bits into buffer; copy from buffer to output in 24-bit chunks
        for currChar in str do
            let currCharCode = int currChar

            if isCharInsideRange currCharCode then
                let decodeVal = configuration.CharCodeToValue.[ currCharCode ]
                if decodeVal >= 0 && not paddingFound then
                    // Decode 1 char chunk into 6 bits
                    bitBuffer <- bitBuffer <<< 6 ||| decodeVal
                    fullChunkBitOffset <- fullChunkBitOffset + 6
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
        // Byte bits [1.....][2.....][3.....]
        // Char bits [1...][2...][3...][4...]
        // Every 4 chars encodes 3 bytes:
        //  1 char = 6 bits = 0 bytes, 6 bits leftover (unused extra char)
        //  2 chars = 12 bits = 1 bytes, 4 bits leftover
        //  3 chars = 18 bits = 2 bytes, 2 bits leftover
        //  4 chars = 24 bits = 3 bytes, 0 bits leftover
        if fullChunkBitOffset > -24 then
            let leftoverBitCount = fullChunkBitOffset + 24
            if leftoverBitCount > 4 then raiseFormatException "invalid input: unused final character"
            let leftoverBitValue = bitBuffer <<< ( 8 - leftoverBitCount ) &&& 0b1111_1111
            if leftoverBitValue > 0 then raiseFormatException "invalid input: extra non-zero bits" // overflow bits should all be zero

        if byteIndex = outBytes.Length then
            outBytes
        else
            Array.truncate byteIndex outBytes

    let [< Literal >] defaultCharacterSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"

open Base64Utils
open System.Runtime.InteropServices

type Base64 private ( configuration : BinaryToTextConfiguration ) =

    static let defaultInstance = lazy Base64( defaultCharacterSet , padding = "=" )

    /// Encodes a byte array into a Base64 string. Optionally wrap output at specified column (will be rounded down to a multiple of 4 for implementation efficiency). Throws exception on invalid input.
    member this.Encode ( bytes : byte array , [< Optional ; DefaultParameterValue( defaultWrapAtColumn ) >] wrapAtColumn : int ) =
        encodeInternal configuration wrapAtColumn bytes
    /// Decodes a Base64 string into a byte array. Throws exception on invalid input.
    member this.Decode ( str : string ) = decodeInternal configuration str
    /// Returns a configuration object describing the character set and newline setting used by this instance.
    member this.Configuration = configuration

    /// Provides a static Base64 encoder/decoder instance using the default options. 4 characters = 3 bytes; 1 character = 6 bits.
    static member Default = defaultInstance.Value

    /// (Default) RFC 4648 section 4: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/
    static member StandardCharacterSet = defaultCharacterSet

    /// RFC 4648 section 5: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_
    static member UrlSafeCharacterSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"

    /// Unix crypt password hashes, ASCII-sortable: ./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz
    static member UnixCryptCharacterSet = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"

    /// <summary>Creates a Base64 encoder/decoder using the specified options. 4 characters = 3 bytes; 1 character = 6 bits.</summary>
    /// <param name='characterSet'>A 64-character string. Characters must be in the range U+0021 to U+007E.
    /// Default: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/</param>
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
            Base64( BinaryToTextConfiguration ( 64 , characterSet , useCrLfNewline , forceCaseSensitive , padding , padOnEncode ) )
