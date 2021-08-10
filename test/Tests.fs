module Tests

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

open Zanaptak.BinaryToTextEncoding
open System

let bytes = [| 0uy ; 255uy ; 0uy ; 255uy ; 0uy ; 255uy ; 0uy ; 255uy |]

// http://base91.sourceforge.net/
let ``legacy rnd1 as hex`` = "1D066A70D432F5E9BAABE8F975DC7120DC64C4EDB1973758D52850F5B6B9B3579764561DB7B15835024B5A621CB44748748B1B476E9F13E22AFF8608E7200203D83222DA60261CB5605AAD68A37766D82966910160B51F1D63D755BF244748B635442A7642D5F5D20A935334C12181E5BF14AAF375939A9BF9C07EA7272707DD8BF79C75A8DE5BD5467697D23719C02799001E5B5E122C795F42D9242089729CF228EEDA1DA6FF89D25DE8C3C3B5BF814E0E9802F9B78A7A572F1132EC2611D2845E71B1EF00653A736B09A2A90736050DFC0BD0134D71104D25C3E67794931BF0DDEF4DDCB602BC51C9F028EA4A3D414121A6A3B0765C1A5B968CD73D3F307AEC0F8378508E387F9A63B3B9FA7C3599D162FFC194554720C4B52DC5993BA3947BFC85173E87066A6B5BFD65B6D2D2F29CFB63C68F6D36A5700058CF9848A6F8D321A278B407900E6762EC6D9931DCB22173C3C629EDFE3836C44670789454E5289D0B725BF40AD43CA3C3E3B4A8A2B053AAC030DDFD5B77CC0E1B369E02B99AD9F3418E9391B58F9D2B31CE287F3A50DED5CA1222FDC1AB73A016016A2759D79201156F7B5EE9C1A01D551CA2F96879D20FDE5ABD88BBD29812A0E78EA7B0C1822A70023A458F4CEF45001E9FC02F56725AD0E90A9075024D9C72FB338331F619CF49B5534B642F56F492C63297D487F3ED2BCA7FE015BA660424CA13CC96CE8A50EAEEE10C4E82DDCAA8AED0B1792CFBF83E1684D18E4AD9F120D0A5F468B170338C8C7C1382FA0582BA7E8DF39059249776BB1BC78BE147CB0D49103CE664671C1C5D2AFE1FEA7015E6E44519174FA15E675AADBF2B0D4F42D93902641E734201A65B306557CA3B8BA48CFF003DA7C1EA"
let ``legacy rnd1 bytes`` = ``legacy rnd1 as hex`` |> Seq.chunkBySize 2 |> Seq.map String |> Seq.map ( fun s -> Convert.ToByte( s , 16 ) ) |> Seq.toArray
let ``legacy rnd1 encoded`` = """SRdJj7WdMpu9rVspWy=C`+p=a<R>M8gO|71mikcp!RYpbTRfTG3zP*LX3Mx/@nAZm}&uOehnuuZu=h|;QGQT"a)cAR.#gmGq6)!wVMDAq^rUzpJ8!zLDJ$<L&w(u:gIl,a!w`(QD#|1O4U)%ilIS>(HVA,CF)&vrl;^:bgUT^=H6>z,>ijx(Dg9ziaN(I0rG<MEyc`;n)p/aX4C]+4hq^#^E^z^Q#yfgf]3]DGci_Z%5![`$H(b4wUTg<tZd_h_tq_vAtwf*~4KNCTxK+N5"C|/22mrAs5mESHgP{N{WWRjOr^a6BQ$R(%VW9/b}NC7CCZ_}xoH#Q;sh&R>P^~Rdw]fLV9vaLSRSN,G`7_;VhS<<a$#hGgra!3r}}RNjZm5DHA/3hwVdIc"Xmq9V95r.9*e$**Qg=L$J~n+<3ywJvJ`ET0"OJaPIk$|qRcnk.)*6<7SiJ6?hFp^s}p5pjvWV#yT._.l+@z98Mj9e=?Q0Z]D,S;>0tZlu7UTcwBn^ffA16WL>r[D{Guz5|JK.&fA6K?~8aM)e(1U"#=^6)Eu4@>c4Q58R,&IA+rV|bek::o9Hbfot/n.mZSYxNs53)d&7Ez2*^`sS~,s7>T={bO4F#H_Sl"H,}hY62HD6B|KS`Z[=&YgPDjR+Os0}~t9/#d3pPX$gBrt*$mbxc@%uO{iL.g~S$KtfJ%`=tTY&8_>P>L&FxRj<}T*/oHAtaKxH,N7cWQhH79Mlj{iPfqec!oyWgyoD0(Ug2O#0EqzZ(*kBp[I)0C"""

let testRandomMultiple encodeFn decodeFn lower upper count =
    let seed = System.Random().Next()
    let r = System.Random( seed )
    for i in 1 .. count do
        let size = r.Next( lower , upper )
        let bytes : byte array = Array.zeroCreate size
        r.NextBytes bytes
        let enc = encodeFn bytes
        let dec = decodeFn enc
        Expect.equal dec bytes ( sprintf "failed with seed %i on index %i" seed i )

let Base16Tests =
    testList "Base16" [
        testCase "encode empty" <| fun () ->
            Expect.equal ( Base16.Default.Encode [||] ) "" ""

        testCase "decode empty" <| fun () ->
            Expect.equal ( Base16.Default.Decode "" ) [||] ""

        testCase "encode default" <| fun () ->
            Expect.equal ( Base16.Default.Encode bytes ) "00FF00FF00FF00FF" ""

        testCase "decode default" <| fun () ->
            Expect.equal ( Base16.Default.Decode "00FF00FF00FF00FF" ) bytes ""

        testCase "encode consonants" <| fun () ->
            Expect.equal ( Base16( Base16.ConsonantsCharacterSet ).Encode bytes ) "BBZZBBZZBBZZBBZZ" ""

        testCase "decode consonants" <| fun () ->
            Expect.equal ( Base16( Base16.ConsonantsCharacterSet ).Decode "BBZZBBZZBBZZBBZZ" ) bytes ""

        testCase "round trip default" <| fun () ->
            testRandomMultiple Base16.Default.Encode Base16.Default.Decode 0 99 999

        testCase "round trip consonants" <| fun () ->
            let codec = Base16( Base16.ConsonantsCharacterSet )
            testRandomMultiple codec.Encode codec.Decode 0 99 999

        testCase "encode wrap crlf default" <| fun () ->
            Expect.equal ( Base16.Default.Encode( bytes , 4 ) ) "00FF\r\n00FF\r\n00FF\r\n00FF" ""

        testCase "encode wrap lf configured" <| fun () ->
            Expect.equal ( Base16( useCrLfNewline = false ).Encode( bytes , 4 ) ) "00FF\n00FF\n00FF\n00FF" ""
    ]

let Base32Tests =
    testList "Base32" [
        testCase "encode empty" <| fun () ->
            Expect.equal ( Base32.Default.Encode [||] ) "" ""

        testCase "decode empty" <| fun () ->
            Expect.equal ( Base32.Default.Decode "" ) [||] ""

        testCase "encode default" <| fun () ->
            Expect.equal ( Base32.Default.Encode bytes ) "AD7QB7YA74AP6" ""

        testCase "decode default" <| fun () ->
            Expect.equal ( Base32.Default.Decode "AD7QB7YA74AP6" ) bytes ""

        testCase "encode consonants" <| fun () ->
            Expect.equal ( Base32( Base32.ConsonantsCharacterSet ).Encode bytes ) "BFzbCznBzsBZx" ""

        testCase "decode consonants" <| fun () ->
            Expect.equal ( Base32( Base32.ConsonantsCharacterSet ).Decode "BFzbCznBzsBZx" ) bytes ""

        testCase "round trip default" <| fun () ->
            testRandomMultiple Base32.Default.Encode Base32.Default.Decode 0 99 999

        testCase "round trip consonants" <| fun () ->
            let codec = Base32( Base32.ConsonantsCharacterSet )
            testRandomMultiple codec.Encode codec.Decode 0 99 999

        testCase "encode wrap crlf default" <| fun () ->
            Expect.equal ( Base32.Default.Encode( bytes , 4 ) ) "AD7Q\r\nB7YA\r\n74AP\r\n6" ""

        testCase "encode wrap lf configured" <| fun () ->
            Expect.equal ( Base32( useCrLfNewline = false ).Encode( bytes , 4 ) ) "AD7Q\nB7YA\n74AP\n6" ""
    ]

let Base46Tests =
    testList "Base46" [
        testCase "encode empty" <| fun () ->
            Expect.equal ( Base46.Default.Encode [||] ) "" ""

        testCase "decode empty" <| fun () ->
            Expect.equal ( Base46.Default.Decode "" ) [||] ""

        testCase "encode default" <| fun () ->
            Expect.equal ( Base46.Default.Encode bytes ) "2CxBH62NvmWD" ""

        testCase "decode default" <| fun () ->
            Expect.equal ( Base46.Default.Decode "2CxBH62NvmWD" ) bytes ""

        testCase "encode letters" <| fun () ->
            Expect.equal ( Base46( Base46.LettersCharacterSet ).Encode bytes ) "AHxGNEASvnZJ" ""

        testCase "decode letters" <| fun () ->
            Expect.equal ( Base46( Base46.LettersCharacterSet ).Decode "AHxGNEASvnZJ" ) bytes ""

        testCase "round trip default" <| fun () ->
            testRandomMultiple Base46.Default.Encode Base46.Default.Decode 0 99 999

        testCase "round trip letters" <| fun () ->
            let codec = Base46( Base46.LettersCharacterSet )
            testRandomMultiple codec.Encode codec.Decode 0 99 999

        testCase "encode wrap crlf default" <| fun () ->
            Expect.equal ( Base46.Default.Encode( bytes , 4 ) ) "2CxB\r\nH62N\r\nvmWD" ""

        testCase "encode wrap lf configured" <| fun () ->
            Expect.equal ( Base46( useCrLfNewline = false ).Encode( bytes , 4 ) ) "2CxB\nH62N\nvmWD" ""

        testCase "encode 11 bytes to 16 chars" <| fun () ->
            let chars16 = String ( Array.create 16 '2' )
            let bytes11 = Array.create 11 0uy
            Expect.equal ( Base46.Default.Encode bytes11 ) chars16 ""

        testCase "decode 16 chars to 11 bytes" <| fun () ->
            let chars16 = String ( Array.create 16 '2' )
            let bytes11 = Array.create 11 0uy
            Expect.equal ( Base46.Default.Decode chars16 ) bytes11 ""
    ]

let Base64Tests =
    testList "Base64" [
        testCase "encode empty" <| fun () ->
            Expect.equal ( Base64.Default.Encode [||] ) "" ""

        testCase "decode empty" <| fun () ->
            Expect.equal ( Base64.Default.Decode "" ) [||] ""

        testCase "encode default" <| fun () ->
            Expect.equal ( Base64.Default.Encode bytes ) "AP8A/wD/AP8" ""

        testCase "decode default" <| fun () ->
            Expect.equal ( Base64.Default.Decode "AP8A/wD/AP8" ) bytes ""

        testCase "encode urlsafe" <| fun () ->
            Expect.equal ( Base64( Base64.UrlSafeCharacterSet ).Encode bytes ) "AP8A_wD_AP8" ""

        testCase "decode urlsafe" <| fun () ->
            Expect.equal ( Base64( Base64.UrlSafeCharacterSet ).Decode "AP8A_wD_AP8" ) bytes ""

        testCase "round trip default" <| fun () ->
            testRandomMultiple Base64.Default.Encode Base64.Default.Decode 0 99 999

        testCase "round trip urlsafe" <| fun () ->
            let codec = Base64( Base64.UrlSafeCharacterSet )
            testRandomMultiple codec.Encode codec.Decode 0 99 999

        testCase "encode wrap crlf default" <| fun () ->
            Expect.equal ( Base64.Default.Encode( bytes , 4 ) ) "AP8A\r\n/wD/\r\nAP8" ""

        testCase "encode wrap lf configured" <| fun () ->
            Expect.equal ( Base64( useCrLfNewline = false ).Encode( bytes , 4 ) ) "AP8A\n/wD/\nAP8" ""
    ]

let Base91Tests =
    testList "Base91" [
        testCase "encode empty" <| fun () ->
            Expect.equal ( Base91.Default.Encode [||] ) "" ""

        testCase "decode empty" <| fun () ->
            Expect.equal ( Base91.Default.Decode "" ) [||] ""

        testCase "encode" <| fun () ->
            Expect.equal ( Base91.Default.Encode bytes ) "!Brm|[Op(Z" ""

        testCase "decode" <| fun () ->
            Expect.equal ( Base91.Default.Decode "!Brm|[Op(Z" ) bytes ""

        testCase "round trip default" <| fun () ->
            testRandomMultiple Base91.Default.Encode Base91.Default.Decode 0 99 999

        testCase "encode wrap crlf default" <| fun () ->
            Expect.equal ( Base91.Default.Encode( bytes , 4 ) ) "!Brm\r\n|[Op\r\n(Z" ""

        testCase "encode wrap lf configured" <| fun () ->
            Expect.equal ( Base91( useCrLfNewline = false ).Encode( bytes , 4 ) ) "!Brm\n|[Op\n(Z" ""

        testCase "encode 13 bytes to 16 chars" <| fun () ->
            let chars16 = String ( Array.create 16 '!' )
            let bytes13 = Array.create 13 0uy
            Expect.equal ( Base91.Default.Encode bytes13 ) chars16 ""

        testCase "decode 16 chars to 13 bytes" <| fun () ->
            let chars16 = String ( Array.create 16 '!' )
            let bytes13 = Array.create 13 0uy
            Expect.equal ( Base91.Default.Decode chars16 ) bytes13 ""
    ]

let Base91LegacyTests =
    testList "Base91Legacy" [
        testCase "encode empty" <| fun () ->
            Expect.equal ( Base91Legacy.Default.Encode [||] ) "" ""

        testCase "decode empty" <| fun () ->
            Expect.equal ( Base91Legacy.Default.Decode "" ) [||] ""

        testCase "encode" <| fun () ->
            Expect.equal ( Base91Legacy.Default.Encode bytes ) "T|2(#A/CmW" ""

        testCase "decode" <| fun () ->
            Expect.equal ( Base91Legacy.Default.Decode "T|2(#A/CmW" ) bytes ""

        testCase "encode rnd1" <| fun () ->
            Expect.equal ( Base91Legacy.Default.Encode ``legacy rnd1 bytes`` ) ``legacy rnd1 encoded`` ""

        testCase "decode rnd1" <| fun () ->
            Expect.equal ( Base91Legacy.Default.Decode ``legacy rnd1 encoded`` ) ``legacy rnd1 bytes`` ""

        testCase "round trip default" <| fun () ->
            testRandomMultiple Base91Legacy.Default.Encode Base91Legacy.Default.Decode 0 99 999
    ]

#if ! FABLE_COMPILER
let Base16ExceptionTests =
    testList "Base16ExceptionTests" [
        testCase "invalid char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base16.Default.Decode "B'C" |> ignore ) ""

        testCase "control char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base16.Default.Decode "B\x1BC" |> ignore ) ""

        testCase "high char outside range throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base16.Default.Decode "B±C" |> ignore ) ""
    ]

let Base32ExceptionTests =
    testList "Base32ExceptionTests" [
        testCase "invalid char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base32.Default.Decode "B'C" |> ignore ) ""

        testCase "control char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base32.Default.Decode "B\x1BC" |> ignore ) ""

        testCase "high char outside range throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base32.Default.Decode "B±C" |> ignore ) ""
    ]

let Base46ExceptionTests =
    testList "Base46ExceptionTests" [
        testCase "invalid char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base46.Default.Decode "B'C" |> ignore ) ""

        testCase "control char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base46.Default.Decode "B\x1BC" |> ignore ) ""

        testCase "high char outside range throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base46.Default.Decode "B±C" |> ignore ) ""

        testCase "throws on 1 char" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base46.Default.Decode "2" |> ignore ) ""

        testCase "throws on 4 chars" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base46.Default.Decode "2222" |> ignore ) ""

        testCase "throws on 7 chars" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base46.Default.Decode "2222222" |> ignore ) ""

        testCase "throws on 10 chars" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base46.Default.Decode "2222222222" |> ignore ) ""

        testCase "throws on 13 chars" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base46.Default.Decode "2222222222222" |> ignore ) ""

        testCase "throws on 17 chars" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base46.Default.Decode "22222222222222222" |> ignore ) ""
    ]

let Base64ExceptionTests =
    testList "Base64ExceptionTests" [
        testCase "invalid char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base64.Default.Decode "B'C" |> ignore ) ""

        testCase "control char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base64.Default.Decode "B\x1BC" |> ignore ) ""

        testCase "high char outside range throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base64.Default.Decode "B±C" |> ignore ) ""
    ]

let Base91ExceptionTests =
    testList "Base91ExceptionTests" [
        testCase "invalid char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base91.Default.Decode "B'C" |> ignore ) ""

        testCase "control char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base91.Default.Decode "B\x1BC" |> ignore ) ""

        testCase "high char outside range throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base91.Default.Decode "B±C" |> ignore ) ""

        testCase "throws on 1 char" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base91.Default.Decode "!" |> ignore ) ""

        testCase "throws on 6 chars" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base91.Default.Decode "!!!!!!" |> ignore ) ""

        testCase "throws on 11 chars" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base91.Default.Decode "!!!!!!!!!!!" |> ignore ) ""

        testCase "throws on 17 chars" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base91.Default.Decode "!!!!!!!!!!!!!!!!!" |> ignore ) ""
    ]

let Base91LegacyExceptionTests =
    testList "Base91LegacyExceptionTests" [
        testCase "invalid char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base91Legacy.Default.Decode "B'C" |> ignore ) ""

        testCase "control char throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base91Legacy.Default.Decode "B\x1BC" |> ignore ) ""

        testCase "high char outside range throws" <| fun () ->
            Expect.throwsT< FormatException > ( fun () -> Base91Legacy.Default.Decode "B±C" |> ignore ) ""
    ]
#endif

let allTests = testList "All" [
    Base16Tests
    Base32Tests
    Base46Tests
    Base64Tests
    Base91Tests
    Base91LegacyTests
#if ! FABLE_COMPILER
    Base16ExceptionTests
    Base32ExceptionTests
    Base46ExceptionTests
    Base64ExceptionTests
    Base91ExceptionTests
    Base91LegacyExceptionTests
#endif
]
