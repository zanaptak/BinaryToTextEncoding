namespace Zanaptak.BinaryToTextEncoding
open Utils

module private Base46Utils =
    open System

    let divisibleBy11 ( value : int ) =
        // 1. / 11. = 0.0909090909090909
        // Increment last digit to ensure whole quotient values are >= floor (e.g. 1.000... instead of 0.999...)
        let quotient = float value * 0.0909090909090910
        quotient - truncate quotient < 0.01

    // Integer reciprocal constant to replace division (slow) with multiplication and shift (fast).
    // (Constant division apparently not optimized by .NET compiler.)
    // Goal: N / 46
    // = N * 1/46
    // = N * 1/46 * 2^x / 2^x
    //    N is 11 bits max, we can multiply a 20 bit number and still fit in 31 bits (ignoring the sign bit)
    //    1/46 approximated in binary is 0.0000010110010000101100100...
    //    1/46 * 2^25 (i.e. move decimal right 25 places) and floored gives us 20 bit integer (leading zeroes trimmed): 10110010000101100100
    //    The truncated multiplication will result in being off by one for integer divisors, so add one to "round up": 10110010000101100101
    // = N * 0b10110010000101100101 / 2^25
    // = N * 0b10110010000101100101 >>> 25
    // Verified correct for all N in 0 .. 2047.
    [< Literal >]
    let private Reciprocal46Shift25 = 0b10110010000101100101

    // Increase by 16/11 ratio
    let inline byteCountToCharCount ( byteCount : int ) =
        let count = float byteCount * 1.45454545454545 |> ceil
        if count <= Int32MaxAsFloat then int count else raiseFormatException "byte count too large"

    // Decrease by 11/16 ratio
    let inline charCountToByteCount ( charCount : int ) =
        float charCount * 0.6875 |> floor |> int

    let encodeInternal ( configuration : BinaryToTextConfiguration ) wrapAtCol ( bytes : byte array ) =
        if isNull bytes then raise ( ArgumentNullException( "bytes" ) )

        let totalChars = byteCountToCharCount bytes.Length

        if totalChars > 0 then

            let wrapAtCol = ( max 0 wrapAtCol ) &&& ~~~3 // restrict to multiple of 4
            let lineFeedChars =
                if wrapAtCol = 0 then 0
                else ( totalChars - 1 ) / wrapAtCol * configuration.Newline.Length
            let outChars : char array = Array.zeroCreate ( totalChars + lineFeedChars )

            let mutable charIndex = 0
            let mutable colIndex = 0
            let mutable bitBuffer = 0
            // This will be 0 or greater when 11 bits or more are in buffer.
            // Used for top level comparison in loop; if 0 or greater then encode 1 chunk.
            // Offset used instead of bitcount to avoid extra subtraction operation when calculating bit shifts.
            let mutable fullChunkBitOffset = -11

            // Add each byte on right side of buffer, shifting previous bits left.
            // Encode leftmost bits of buffer when enough exist for a character chunk.
            for byte in bytes do
                bitBuffer <- bitBuffer <<< 8 ||| int byte
                fullChunkBitOffset <- fullChunkBitOffset + 8
                // Encode 11-bit chunk into 2 chars
                if fullChunkBitOffset >= 0 then
                    if wrapAtCol > 0 && colIndex >= wrapAtCol then
                        Array.Copy( configuration.Newline , 0 , outChars , charIndex , configuration.Newline.Length )
                        charIndex <- charIndex + configuration.Newline.Length
                        colIndex <- 0
                    let bitValue = bitBuffer >>> fullChunkBitOffset &&& 0b111_1111_1111
                    let hiCharVal = bitValue * Reciprocal46Shift25 >>> 25
                    outChars.[ charIndex ] <- configuration.ValueToChar.[ hiCharVal ]
                    outChars.[ charIndex + 1 ] <- configuration.ValueToChar.[ bitValue - ( hiCharVal * 46 ) ]
                    charIndex <- charIndex + 2
                    colIndex <- colIndex + 2
                    fullChunkBitOffset <- fullChunkBitOffset - 11

            // Encode final partial chunk if necessary
            let remainingBitCount = fullChunkBitOffset + 11
            if remainingBitCount >= 6 then
                // 2 chars for 6-11 bits (shift left and mask to encode as big endian)
                if wrapAtCol > 0 && colIndex >= wrapAtCol then
                    Array.Copy( configuration.Newline , 0 , outChars , charIndex , configuration.Newline.Length )
                    charIndex <- charIndex + configuration.Newline.Length
                let bitValue = bitBuffer <<< ( 11 - remainingBitCount ) &&& 0b111_1111_1111
                let hiCharVal = bitValue * Reciprocal46Shift25 >>> 25
                outChars.[ charIndex ] <- configuration.ValueToChar.[ hiCharVal ]
                outChars.[ charIndex + 1 ] <- configuration.ValueToChar.[ bitValue - ( hiCharVal * 46 ) ]
            elif remainingBitCount > 0 then
                // 1 char for 1-5 bits (shift left and mask to encode as big endian)
                if wrapAtCol > 0 && colIndex >= wrapAtCol then
                    Array.Copy( configuration.Newline , 0 , outChars , charIndex , configuration.Newline.Length )
                    charIndex <- charIndex + configuration.Newline.Length
                let bitValue = bitBuffer <<< ( 5 - remainingBitCount ) &&& 0b1_1111
                let hiCharVal = bitValue * Reciprocal46Shift25 >>> 25
                outChars.[ charIndex ] <- configuration.ValueToChar.[ bitValue - ( hiCharVal * 46 ) ]

            String outChars

        else ""

    let decodeInternal ( configuration : BinaryToTextConfiguration ) ( str : string ) =
        if isNull str then raise ( ArgumentNullException( "str" ) )

        let inputLength = usableLength str

        let outBytes : byte array = Array.zeroCreate ( charCountToByteCount inputLength )

        let mutable prevCharCode = 0
        let mutable byteIndex = 0
        let mutable bitBuffer = 0
        let mutable fullChunkBitOffset = -16

        // Decode each 2-char chunk as 11 bits into buffer; copy from buffer to output in 16-bit chunks
        for currChar in str do
            let currCharCode = int currChar
            if isCharInsideRange currCharCode then
                if prevCharCode = 0 then
                    prevCharCode <- currCharCode
                else
                    // Decode 2 char chunk into 11 bits
                    let hiDecode , loDecode = configuration.CharCodeToValue.[ prevCharCode ] , configuration.CharCodeToValue.[ currCharCode ]
                    if hiDecode <> InvalidDecode && loDecode <> InvalidDecode then
                        bitBuffer <- bitBuffer <<< 11 ||| ( hiDecode * 46 + loDecode )
                        fullChunkBitOffset <- fullChunkBitOffset + 11
                        prevCharCode <- 0
                        // Copy 16 bits into 2 bytes
                        if fullChunkBitOffset >= 0 then
                            let bitValue = bitBuffer >>> fullChunkBitOffset
                            outBytes.[ byteIndex ] <- bitValue >>> 8 |> byte
                            outBytes.[ byteIndex + 1 ] <- bitValue |> byte
                            byteIndex <- byteIndex + 2
                            fullChunkBitOffset <- fullChunkBitOffset - 16
                    elif hiDecode = InvalidDecode then raiseFormatException "invalid input: invalid char '%c'" ( char prevCharCode )
                    elif loDecode = InvalidDecode then raiseFormatException "invalid input: invalid char '%c'" ( char currCharCode )
            elif not ( isCharWhitespace currCharCode ) then raiseFormatException "invalid input: invalid char code 0x%x" currCharCode

        // Decode 1 trailing char as 5 bits into buffer
        if prevCharCode <> 0 then
            let loDecode = configuration.CharCodeToValue.[ prevCharCode ]
            if loDecode <> InvalidDecode then
                bitBuffer <- bitBuffer <<< 5 ||| loDecode
                fullChunkBitOffset <- fullChunkBitOffset + 5
            else raiseFormatException "invalid input: invalid char '%c'" ( char prevCharCode )

        // Copy any remaining 8-bit chunks to output
        while fullChunkBitOffset >= -8 do
            let bitValue = bitBuffer >>> ( fullChunkBitOffset + 8 )
            outBytes.[ byteIndex ] <- bitValue |> byte
            byteIndex <- byteIndex + 1
            fullChunkBitOffset <- fullChunkBitOffset - 8

        // Sanity check leftover bits.
        // Raise error if leftover bits are invalid; we don't want multiple strings mapping to the same binary value.
        // Byte bits [1.....][2.....][3.....][4.....][5.....][6.....][7.....][8.....][9.....][10....][11....]
        // Char bits [1..][2...][3..][4...][5..][6...][7..][8...][9..][10..][11.][12..][13.][14..][15.][16..]
        // Every 16 chars encodes 11 bytes:
        //  1 char = 5 bits = 0 bytes, 5 bits leftover (unused extra char)
        //  2 chars = 11 bits = 1 bytes, 3 bits leftover
        //  3 chars = 16 bits = 2 bytes, 0 bits leftover
        //  4 chars = 22 bits = 2 bytes, 6 bits leftover (unused extra char)
        //  5 chars = 27 bits = 3 bytes, 3 bits leftover
        //  6 chars = 33 bits = 4 bytes, 1 bits leftover
        //  7 chars = 38 bits = 4 bytes, 6 bits leftover (unused extra char)
        //  8 chars = 44 bits = 5 bytes, 4 bits leftover
        //  9 chars = 49 bits = 6 bytes, 1 bits leftover
        //  10 chars = 55 bits = 6 bytes, 7 bits leftover (unused extra char)
        //  11 chars = 60 bits = 7 bytes, 4 bits leftover
        //  12 chars = 66 bits = 8 bytes, 2 bits leftover
        //  13 chars = 71 bits = 8 bytes, 7 bits leftover (unused extra char)
        //  14 chars = 77 bits = 9 bytes, 5 bits leftover
        //  15 chars = 82 bits = 10 bytes, 2 bits leftover
        //  16 chars = 88 bits = 11 bytes, 0 bits leftover
        if fullChunkBitOffset > -16 then
            let leftoverBitCount = fullChunkBitOffset + 16
            if leftoverBitCount >= 6 || ( leftoverBitCount = 5 && divisibleBy11 byteIndex ) then raiseFormatException "invalid input: extra char"
            let leftoverBitValue = bitBuffer <<< ( 8 - leftoverBitCount ) &&& 0b1111_1111
            if leftoverBitValue > 0 then raiseFormatException "invalid input: extra non-zero bits" // overflow bits should all be zero

        outBytes

    let [< Literal >] defaultCharacterSet = "234567BCDFGHJKMNPQRSTVWXYZbcdfghjkmnpqrstvwxyz"

open Base46Utils
open System.Runtime.InteropServices

type Base46 private ( configuration : BinaryToTextConfiguration ) =

    static let defaultInstance = Base46( defaultCharacterSet )

    /// Encodes a byte array into a Base46 string. Optionally wrap output at specified column; for efficiency, column must be a multiple of 4, will be rounded down if necessary. Throws exception on invalid input.
    member this.Encode ( bytes : byte array , [< Optional ; DefaultParameterValue( 0 ) >] wrapAtColumn : int ) = encodeInternal configuration wrapAtColumn bytes
    /// Decodes a Base46 string into a byte array. Throws exception on invalid input.
    member this.Decode ( str : string ) = decodeInternal configuration str
    /// Returns a configuration object describing the character set and newline setting used by this instance.
    member this.Configuration = configuration

    /// Provides a static Base46 encoder/decoder instance using the default options. 16 characters = 11 bytes; 1 character = 5.5 bits.
    static member Default = defaultInstance
    /// Characters: 234567BCDFGHJKMNPQRSTVWXYZbcdfghjkmnpqrstvwxyz
    static member SortableCharacterSet = defaultCharacterSet

    /// <summary>Creates a Base46 encoder/decoder using the specified options. 16 characters = 11 bytes; 1 character = 5.5 bits.</summary>
    /// <param name='characterSet'>A 46-character string. Characters must be in the range U+0021 to U+007E.
    /// Default: 234567BCDFGHJKMNPQRSTVWXYZbcdfghjkmnpqrstvwxyz</param>
    /// <param name='useCrLfNewline'>Specifies whether to use CRLF (true) or LF (false) when encoding with the wrap option. Default: true</param>
    new
        (
            [< Optional ; DefaultParameterValue( defaultCharacterSet ) >] characterSet : string
            , [< Optional ; DefaultParameterValue( true ) >] useCrLfNewline : bool
        ) =
            Base46( BinaryToTextConfiguration ( 46 , characterSet , useCrLfNewline ) )
