namespace Zanaptak.BinaryToTextEncoding
open Utils

module private Base91LegacyUtils =
    open System

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

    // Increase by 16/13 ratio (allow space for worst case conversion)
    let inline byteCountToCharCountLegacy ( byteCount : int ) =
        let count = float byteCount * 1.23076923076923 |> ceil
        if count <= Int32MaxAsFloat then int count else raiseFormatException "byte count too large"

    // Decrease by 14/16 ratio (allow space for worst case conversion)
    let inline charCountToByteCountLegacy ( charCount : int ) =
        float charCount * 0.875 |> floor |> int

    let encodeInternal ( configuration : BinaryToTextConfiguration ) wrapAtCol ( bytes : byte array ) =
        if isNull bytes then raise ( ArgumentNullException( "bytes" ) )

        let totalChars = byteCountToCharCountLegacy bytes.Length

        if totalChars > 0 then

            let wrapAtCol = ( max 0 wrapAtCol ) &&& ~~~3 // restrict to multiple of 4
            let lineFeedChars =
                if wrapAtCol = 0 then 0
                else ( totalChars - 1 ) / wrapAtCol * configuration.Newline.Length
            let outChars : char array = Array.zeroCreate ( totalChars + lineFeedChars )

            let mutable charIndex = 0
            let mutable colIndex = 0
            let mutable bitBuffer = 0
            let mutable bitCount = 0

            let inline wrapIfNeeded() =
                if wrapAtCol > 0 && colIndex >= wrapAtCol then
                    Array.Copy( configuration.Newline , 0 , outChars , charIndex , configuration.Newline.Length )
                    charIndex <- charIndex + configuration.Newline.Length
                    colIndex <- 0

            for byte in bytes do
                bitBuffer <- byte |> int <<< bitCount ||| bitBuffer
                bitCount <- bitCount + 8
                // Encode each 13-14 bit chunk into 2 chars
                // 13 bits = 8192 values
                // We have 91*91 = 8281 2-char combinations to use (0 .. 8280)
                // Max value 8280 = 10_0000_0101_1000, so if lower 13 bits <= 0_0000_0101_1000 (88),
                //    then we can also represent 14th bit whether it is 0 or 1
                //    (if 14th bit is 0, value will be <= 88; if 1, value will be >= 8192 up to the max of 8280)
                if bitCount > 13 then
                    wrapIfNeeded()
                    let bitValue = bitBuffer &&& 0b1_1111_1111_1111 // 13 bits
                    if bitValue > 88 then
                        let hiCharVal = bitValue * Reciprocal91Shift24 >>> 24
                        outChars.[ charIndex ] <- configuration.ValueToChar.[ bitValue - ( hiCharVal * 91 ) ] // low digit first in legacy algorithm
                        outChars.[ charIndex + 1 ] <- configuration.ValueToChar.[ hiCharVal ]
                        bitBuffer <- bitBuffer >>> 13
                        bitCount <- bitCount - 13
                    else
                        let bitValue14 = bitBuffer &&& 0b11_1111_1111_1111 // 14 bits
                        let hiCharVal = bitValue14 * Reciprocal91Shift24 >>> 24
                        outChars.[ charIndex ] <- configuration.ValueToChar.[ bitValue14 - ( hiCharVal * 91 ) ] // low digit first in legacy algorithm
                        outChars.[ charIndex + 1 ] <- configuration.ValueToChar.[ hiCharVal ]
                        bitBuffer <- bitBuffer >>> 14
                        bitCount <- bitCount - 14

                    charIndex <- charIndex + 2
                    colIndex <- colIndex + 2

            // Encode final partial chunk if necessary
            if bitCount >= 7 then
                // 2 chars for 7-13 bits
                wrapIfNeeded()
                let hiCharVal = bitBuffer * Reciprocal91Shift24 >>> 24
                outChars.[ charIndex ] <- configuration.ValueToChar.[ bitBuffer - ( hiCharVal * 91 ) ] // low digit first in legacy algorithm
                outChars.[ charIndex + 1 ] <- configuration.ValueToChar.[ hiCharVal ]
                charIndex <- charIndex + 2
            elif bitCount > 0 then
                // 1 char for 1-6 bits
                wrapIfNeeded()
                let hiCharVal = bitBuffer * Reciprocal91Shift24 >>> 24
                outChars.[ charIndex ] <- configuration.ValueToChar.[ bitBuffer - ( hiCharVal * 91 ) ] // final trailing low digit
                charIndex <- charIndex + 1

            if charIndex = outChars.Length then
                String outChars
            else
                String( outChars , 0 , charIndex )

        else ""

    let decodeInternal ( configuration : BinaryToTextConfiguration ) ( str : string ) =
        if isNull str then raise ( ArgumentNullException( "str" ) )

        let outBytes : byte array = Array.zeroCreate ( charCountToByteCountLegacy str.Length )

        let mutable byteIndex = 0
        let mutable prevCharCode = 0
        let mutable bitBuffer = 0
        let mutable bitCount = 0

        for currChar in str do
            let currCharCode = int currChar
            if isCharInsideRange currCharCode then
                if prevCharCode = 0 then
                    prevCharCode <- currCharCode
                else
                    // Decode 2 char chunk into 13-14 bits
                    let loDecode , hiDecode = configuration.CharCodeToValue.[ prevCharCode ] , configuration.CharCodeToValue.[ currCharCode ]
                    if hiDecode <> InvalidChar && loDecode <> InvalidChar then
                        let bitValue = hiDecode * 91 + loDecode // all char pairs used in legacy algorithm, no need to sanity check value
                        bitBuffer <- bitValue <<< bitCount ||| bitBuffer
                        if bitValue &&& 0b1_1111_1111_1111 > 88 then
                            bitCount <- bitCount + 13
                        else
                            bitCount <- bitCount + 14
                        prevCharCode <- 0
                        while bitCount >= 8 do
                            outBytes.[ byteIndex ] <- bitBuffer &&& 0b1111_1111 |> byte
                            byteIndex <- byteIndex + 1
                            bitBuffer <- bitBuffer >>> 8
                            bitCount <- bitCount - 8
                    elif loDecode = InvalidChar then raiseFormatException "invalid input: invalid character '%c'" ( char prevCharCode )
                    elif hiDecode = InvalidChar then raiseFormatException "invalid input: invalid character '%c'" ( char currCharCode )
            elif not ( isCharWhitespace currCharCode ) then raiseFormatException "invalid input: invalid character code 0x%x" currCharCode

        if prevCharCode <> 0 then
            // Decode 1 trailing char into 6 bits
            let loDecode = configuration.CharCodeToValue.[ prevCharCode ]
            if loDecode <> InvalidChar && loDecode <= 0b11_1111 then
                bitBuffer <- loDecode &&& 0b11_1111 <<< bitCount ||| bitBuffer
                bitCount <- bitCount + 6
                if bitCount >= 8 then
                    outBytes.[ byteIndex ] <- bitBuffer &&& 0b1111_1111 |> byte
                    byteIndex <- byteIndex + 1
                    bitBuffer <- bitBuffer >>> 8
            elif loDecode > 0b11_1111 then
                raiseFormatException "invalid input: bit value too high for final character '%c'" ( char prevCharCode )
            else raiseFormatException "invalid input: invalid character '%c'" ( char prevCharCode )

        if bitBuffer > 0 then raiseFormatException "invalid input: extra non-zero bits" // overflow bits should all be zero
        if byteIndex = outBytes.Length then
            outBytes
        else
            Array.truncate byteIndex outBytes

    let [< Literal >] defaultCharacterSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&()*+,./:;<=>?@[]^_`{|}~\""

open Base91LegacyUtils
open System.Runtime.InteropServices

type Base91Legacy private ( configuration : BinaryToTextConfiguration ) =

    static let defaultInstance = lazy Base91Legacy( defaultCharacterSet )

    /// Encodes a byte array into a legacy `basE91` string. Optionally wrap output at specified column (will be rounded down to a multiple of 4 for implementation efficiency). Throws exception on invalid input.
    member this.Encode ( bytes : byte array , [< Optional ; DefaultParameterValue( defaultWrapAtColumn ) >] wrapAtColumn : int ) =
        encodeInternal configuration wrapAtColumn bytes
    /// Decodes a legacy `basE91` string into a byte array. Throws exception on invalid input.
    member this.Decode ( str : string ) = decodeInternal configuration str
    /// Returns a configuration object describing the character set and newline setting used by this instance.
    member this.Configuration = configuration

    /// Provides a static legacy `basE91` encoder/decoder instance using the default options. 16 characters = 13-14 bytes; 1 character = 6.5-7 bits. (Note: Legacy `basE91` algorithm, incompatible with updated algorithm.)
    static member Default = defaultInstance.Value

    /// (Default) Original 'basE91' character set: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&()*+,./:;<=>?@[]^_`{|}~"
    static member LegacyCharacterSet = defaultCharacterSet

    /// <summary>Creates a legacy `basE91` encoder/decoder using the specified options. 16 characters = 13-14 bytes; 1 character = 6.5-7 bits. (Note: Legacy `basE91` algorithm, incompatible with updated algorithm.)</summary>
    /// <param name='characterSet'>A 91-character string. Characters must be in the range U+0021 to U+007E.
    /// Default: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&amp;()*+,./:;&lt;=&gt;?@[]^_`{|}~&quot;</param>
    /// <param name='useCrLfNewline'>Specifies whether to use CRLF (true) or LF (false) when encoding with the wrap option. Default: true</param>
    new
        (
            [< Optional ; DefaultParameterValue( defaultCharacterSet ) >] characterSet : string
            , [< Optional ; DefaultParameterValue( defaultUseCrLfNewline ) >] useCrLfNewline : bool
        ) =
            Base91Legacy( BinaryToTextConfiguration ( 91 , characterSet , useCrLfNewline , true ) )
