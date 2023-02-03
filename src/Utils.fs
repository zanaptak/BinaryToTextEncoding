namespace Zanaptak.BinaryToTextEncoding
open System

module internal Utils =

    let raiseFormatException fmt =
        fmt |> Printf.kprintf ( fun s -> raise ( FormatException( s ) ) )

    let inline isCharInsideRange charCode =
        charCode >= 0x21 && charCode <= 0x7E

    let inline isCharWhitespace charCode =
        charCode = 0xA || charCode = 0xD || charCode = 0x9 || charCode = 0x20

    let inline isCharLowercase charCode =
        charCode >= 0x61 && charCode <= 0x7a

    let inline isCharUppercase charCode =
        charCode >= 0x41 && charCode <= 0x5a

    let inline toLowercase charCode =
        if isCharUppercase charCode then charCode + 32 else charCode

    let inline toUppercase charCode =
        if isCharLowercase charCode then charCode - 32 else charCode

    let hasRepeatLetters characters =
        let distinctIgnoringCase =
            characters
            |> Array.map( fun c -> toLowercase( int c ) )
            |> Array.distinct
        distinctIgnoringCase.Length <> characters.Length

    let [< Literal >] InvalidChar = -1
    let [< Literal >] PaddingChar = -2
    let [< Literal >] Int32MaxAsFloat = 2147483647.
    let [< Literal >] defaultWrapAtColumn = 0
    let [< Literal >] defaultUseCrLfNewline = true
    let [< Literal >] defaultForceCaseSensitive = false
    let [< Literal >] defaultPadding = ""
    let [< Literal >] defaultPadOnEncode = false

open Utils

type BinaryToTextConfiguration internal (
    charCount
    , characterSet : string
    , useCrLfNewline
    , forceCaseSensitive
    , ?padding
    , ?padOnEncode
) =
    let padding = defaultArg padding defaultPadding
    let padOnEncode = defaultArg padOnEncode defaultPadOnEncode

    do if String.IsNullOrWhiteSpace characterSet then raise ( ArgumentNullException( "characterSet" ) )

    let valueToChar =
        characterSet
        |> Seq.filter ( fun c -> isCharInsideRange ( int c ) )
        |> Seq.distinct
        |> Seq.toArray

    let isCaseSensitive = forceCaseSensitive || hasRepeatLetters valueToChar

    do
        if valueToChar.Length <> charCount then
            let message = sprintf "character set must be %i distinct chars in the range U+0021 to U+007E" charCount
            raise ( ArgumentException( message , "characterSet" ) )

    let characterSet = String valueToChar
    let charCodeToValue = Array.replicate 0x7F InvalidChar

    do
        valueToChar
        |> Array.iteri ( fun i c ->
            if isCaseSensitive then
                // Set value for specified character only
                charCodeToValue.[ int c ] <- i
            else
                // Set value for both uppercase and lowercase (if non-letter, will just set same index twice)
                charCodeToValue.[ toUppercase( int c ) ] <- i
                charCodeToValue.[ toLowercase( int c ) ] <- i
        )

    let paddingCharacter =
        if not( String.IsNullOrEmpty padding ) && padding <> defaultPadding then
            if padding.Length <> 1 then
                raise ( ArgumentException( "padding must be a single character string" , "padding" ) )

            let charCode = int( padding.[ 0 ] )

            if not( isCharInsideRange( charCode ) ) then
                raise ( ArgumentException( "padding character must be in the range U+0021 to U+007E" , "padding" ) )

            // padding check intentionally done after above case-sensitivity determination,
            // user will have to force case sensitivity if they want to reuse a letter with
            // a different case as padding
            if charCodeToValue.[ charCode ] >= 0 then
                raise ( ArgumentException( "padding character must not exist in character set" , "padding" ) )

            Some padding.[ 0 ]
        else None

    do
        match paddingCharacter with
        | Some c ->
            if isCaseSensitive then
                // Set value for specified character only
                charCodeToValue.[ int c ] <- PaddingChar
            else
                // Set value for both uppercase and lowercase (if non-letter, will just set same index twice)
                charCodeToValue.[ toUppercase( int c ) ] <- PaddingChar
                charCodeToValue.[ toLowercase( int c ) ] <- PaddingChar
        | None -> ()

    do
        if padOnEncode && Option.isNone paddingCharacter then
            raise ( ArgumentException( "padOnEncode parameter requires valid padding parameter" , "padOnEncode" ) )

    let newline = if useCrLfNewline then [| '\r' ; '\n' |] else [| '\n' |]

    /// Array mapping numeric values of bits to their character representation.
    member internal this.ValueToChar = valueToChar
    /// Array mapping character code to numeric value of bits it represents.
    /// Values less than zero indicate padding or invalid characters.
    member internal this.CharCodeToValue = charCodeToValue
    member internal this.Newline = newline
    member internal this.PaddingCharacter = paddingCharacter
    member this.CharacterSet = characterSet
    member this.UseCrLfNewline = useCrLfNewline
    member this.IsCaseSensitive = isCaseSensitive
    member this.Padding = padding
    member this.PadOnEncode = padOnEncode
