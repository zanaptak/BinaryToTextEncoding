namespace Zanaptak.BinaryToTextEncoding
open Utils

module private Base91Utils =
    open System

    let divisibleBy13 ( value : int ) =
        // 1. / 13. = 0.0769230769230769
        // Increment last digit to ensure whole quotient values are >= floor (e.g. 1.000... instead of 0.999...)
        let quotient = float value * 0.0769230769230770
        quotient - truncate quotient < 0.01

    // Integer reciprocal constant to replace division (slow) with multiplication and shift (fast).
    // (Constant division apparently not optimized by .NET compiler.)
    // Goal: N / 91
    // = N * 1/91
    // = N * 1/91 * 2^x / 2^x
    //    N is 13 bits max, we can multiply an 18 bit number and still fit in 31 bits (ignoring the sign bit)
    //    1/91 approximated in binary is 0.000000101101000000101101000000101101...
    //    1/91 * 2^24 (i.e. move decimal right 24 places) and floored gives us 18 bit integer (leading zeroes trimmed): 101101000000101101
    //    The truncated multiplication will result in being off by one for integer divisors, so add one to "round up": 101101000000101110
    // = N * 0b101101000000101110 / 2^24
    // = N * 0b101101000000101110 >>> 24
    // Verified correct for all N in 0 .. 8191.
    [< Literal >]
    let private Reciprocal91Shift24 = 0b101101000000101110

    // Increase by 16/13 ratio
    let inline byteCountToCharCount ( byteCount : int ) =
        let count = float byteCount * 1.23076923076923 |> ceil
        if count <= Int32MaxAsFloat then int count else raiseFormatException "byte count too large"

    // Decrease by 13/16 ratio
    let inline charCountToByteCount ( charCount : int ) =
        float charCount * 0.8125 |> floor |> int

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
            // This will be 0 or greater when 13 bits or more are in buffer.
            // Used for top level comparison in loop; if 0 or greater then encode 1 chunk.
            // Offset used instead of bitcount to avoid extra subtraction operation when calculating bit shifts.
            let mutable fullChunkBitOffset = -13

            // Add each byte on right side of buffer, shifting previous bits left.
            // Encode leftmost bits of buffer when enough exist for a character chunk.
            for byte in bytes do
                bitBuffer <- bitBuffer <<< 8 ||| int byte
                fullChunkBitOffset <- fullChunkBitOffset + 8
                // Encode 13-bit chunk into 2 chars
                if fullChunkBitOffset >= 0 then
                    if wrapAtCol > 0 && colIndex >= wrapAtCol then
                        Array.Copy( configuration.Newline , 0 , outChars , charIndex , configuration.Newline.Length )
                        charIndex <- charIndex + configuration.Newline.Length
                        colIndex <- 0
                    let bitValue = bitBuffer >>> fullChunkBitOffset &&& 0b1_1111_1111_1111
                    let hiCharVal = bitValue * Reciprocal91Shift24 >>> 24
                    outChars.[ charIndex ] <- configuration.ValueToChar.[ hiCharVal ]
                    outChars.[ charIndex + 1 ] <- configuration.ValueToChar.[ bitValue - ( hiCharVal * 91 ) ]
                    charIndex <- charIndex + 2
                    colIndex <- colIndex + 2
                    fullChunkBitOffset <- fullChunkBitOffset - 13

            // Encode final partial chunk if necessary
            let remainingBitCount = fullChunkBitOffset + 13
            if remainingBitCount >= 7 then
                // 2 chars for 7-13 bits (shift left and mask to encode as big endian)
                if wrapAtCol > 0 && colIndex >= wrapAtCol then
                    Array.Copy( configuration.Newline , 0 , outChars , charIndex , configuration.Newline.Length )
                    charIndex <- charIndex + configuration.Newline.Length
                let bitValue = bitBuffer <<< ( 13 - remainingBitCount ) &&& 0b1_1111_1111_1111
                let hiCharVal = bitValue * Reciprocal91Shift24 >>> 24
                outChars.[ charIndex ] <- configuration.ValueToChar.[ hiCharVal ]
                outChars.[ charIndex + 1 ] <- configuration.ValueToChar.[ bitValue - ( hiCharVal * 91 ) ]
            elif remainingBitCount > 0 then
                // 1 char for 1-6 bits (shift left and mask to encode as big endian)
                if wrapAtCol > 0 && colIndex >= wrapAtCol then
                    Array.Copy( configuration.Newline , 0 , outChars , charIndex , configuration.Newline.Length )
                    charIndex <- charIndex + configuration.Newline.Length
                let bitValue = bitBuffer <<< ( 6 - remainingBitCount ) &&& 0b11_1111
                let hiCharVal = bitValue * Reciprocal91Shift24 >>> 24
                outChars.[ charIndex ] <- configuration.ValueToChar.[ bitValue - ( hiCharVal * 91 ) ]

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

        // Decode each 2-char chunk as 13 bits into buffer; copy from buffer to output in 16-bit chunks
        for currChar in str do
            let currCharCode = int currChar
            if isCharInsideRange currCharCode then
                if prevCharCode = 0 then
                    prevCharCode <- currCharCode
                else
                    // Decode 2 char chunk into 13 bits
                    let hiDecode , loDecode = configuration.CharCodeToValue.[ prevCharCode ] , configuration.CharCodeToValue.[ currCharCode ]
                    if hiDecode <> InvalidDecode && loDecode <> InvalidDecode then
                        bitBuffer <- bitBuffer <<< 13 ||| ( hiDecode * 91 + loDecode )
                        fullChunkBitOffset <- fullChunkBitOffset + 13
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

        // Decode 1 trailing char as 6 bits into buffer
        if prevCharCode <> 0 then
            let loDecode = configuration.CharCodeToValue.[ prevCharCode ]
            if loDecode <> InvalidDecode then
                bitBuffer <- bitBuffer <<< 6 ||| loDecode
                fullChunkBitOffset <- fullChunkBitOffset + 6
            else raiseFormatException "invalid input: invalid char '%c'" ( char prevCharCode )

        // Copy any remaining 8-bit chunks to output
        while fullChunkBitOffset >= -8 do
            let bitValue = bitBuffer >>> ( fullChunkBitOffset + 8 )
            outBytes.[ byteIndex ] <- bitValue |> byte
            byteIndex <- byteIndex + 1
            fullChunkBitOffset <- fullChunkBitOffset - 8

        // Sanity check leftover bits.
        // Raise error if leftover bits are invalid; we don't want multiple strings mapping to the same binary value.
        // Byte bits [1.....][2.....][3.....][4.....][5.....][6.....][7.....][8.....][9.....][10....][11....][12....][13....]
        // Char bits [1...][2....][3...][4....][5...][6....][7...][8....][9...][10...][11..][12...][13..][14...][15..][16...]
        // Every 16 chars encodes 13 bytes:
        //  1 char = 6 bits = 0 bytes, 6 bits leftover (unused extra char)
        //  2 chars = 13 bits = 1 bytes, 5 bits leftover
        //  3 chars = 19 bits = 2 bytes, 3 bits leftover
        //  4 chars = 26 bits = 3 bytes, 2 bits leftover
        //  5 chars = 32 bits = 4 bytes, 0 bits leftover
        //  6 chars = 39 bits = 4 bytes, 7 bits leftover (unused extra char)
        //  7 chars = 45 bits = 5 bytes, 5 bits leftover
        //  8 chars = 52 bits = 6 bytes, 4 bits leftover
        //  9 chars = 58 bits = 7 bytes, 2 bits leftover
        //  10 chars = 65 bits = 8 bytes, 1 bits leftover
        //  11 chars = 71 bits = 8 bytes, 7 bits leftover (unused extra char)
        //  12 chars = 78 bits = 9 bytes, 6 bits leftover
        //  13 chars = 84 bits = 10 bytes, 4 bits leftover
        //  14 chars = 91 bits = 11 bytes, 3 bits leftover
        //  15 chars = 97 bits = 12 bytes, 1 bits leftover
        //  16 chars = 104 bits = 13 bytes, 0 bits leftover
        if fullChunkBitOffset > -16 then
            let leftoverBitCount = fullChunkBitOffset + 16
            if leftoverBitCount = 7 || ( leftoverBitCount = 6 && divisibleBy13 byteIndex ) then raiseFormatException "invalid input: extra char"
            let leftoverBitValue = bitBuffer <<< ( 8 - leftoverBitCount ) &&& 0b1111_1111
            if leftoverBitValue > 0 then raiseFormatException "invalid input: extra non-zero bits" // overflow bits should all be zero

        outBytes

    let [< Literal >] defaultCharacterSet = "!#$%&()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]^_`abcdefghijklmnopqrstuvwxyz{|}~"

open Base91Utils
open System.Runtime.InteropServices

type Base91 private ( configuration : BinaryToTextConfiguration ) =

    static let defaultInstance = Base91( defaultCharacterSet )

    /// Encodes a byte array into a Base91 string. Optionally wrap output at specified column (will be rounded down to a multiple of 4 for implementation efficiency). Throws exception on invalid input.
    member this.Encode ( bytes : byte array , [< Optional ; DefaultParameterValue( 0 ) >] wrapAtColumn : int ) = encodeInternal configuration wrapAtColumn bytes
    /// Decodes a Base91 string into a byte array. Throws exception on invalid input.
    member this.Decode ( str : string ) = decodeInternal configuration str
    /// Returns a configuration object describing the character set and newline setting used by this instance.
    member this.Configuration = configuration

    /// Provides a static Base91 encoder/decoder instance using the default options. 16 characters = 13 bytes; 1 character = 6.5 bits. (Note: Updated algorithm, incompatible with legacy `basE91` algorithm.)
    static member Default = defaultInstance
    /// Characters: !#$%&()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]^_`abcdefghijklmnopqrstuvwxyz{|}~
    static member SortableQuotableCharacterSet = defaultCharacterSet

    /// <summary>Creates a Base91 encoder/decoder using the specified options. 16 characters = 13 bytes; 1 character = 6.5 bits. (Note: Updated algorithm, incompatible with legacy `basE91` algorithm.)</summary>
    /// <param name='characterSet'>A 91-character string. Characters must be in the range U+0021 to U+007E.
    /// Default: !#$%&amp;()*+,-./0123456789:;&lt;=&gt;?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]^_`abcdefghijklmnopqrstuvwxyz{|}~</param>
    /// <param name='useCrLfNewline'>Specifies whether to use CRLF (true) or LF (false) when encoding with the wrap option. Default: true</param>
    new
        (
            [< Optional ; DefaultParameterValue( defaultCharacterSet ) >] characterSet : string
            , [< Optional ; DefaultParameterValue( true ) >] useCrLfNewline : bool
        ) =
            Base91( BinaryToTextConfiguration ( 91 , characterSet , useCrLfNewline ) )
