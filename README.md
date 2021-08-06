# Zanaptak.BinaryToTextEncoding

[![GitHub](https://img.shields.io/badge/-github-gray?logo=github)](https://github.com/zanaptak/BinaryToTextEncoding) [![NuGet](https://img.shields.io/nuget/v/Zanaptak.BinaryToTextEncoding?logo=nuget)](https://www.nuget.org/packages/Zanaptak.BinaryToTextEncoding)

A binary-to-text encoder/decoder library for .NET and Fable. Provides base 16, base 32, base 46, base 64, and base 91 codecs. Supports custom character sets.

## Output example

Example of a random 16-byte array (same size as a GUID) encoded in each base:

- Base 16: `3A319D0D6BA340E8CFFA6E8F65236B71`
- Base 32: `HIYZ2DLLUNAORT72N2HWKI3LOE`
- Base 46: `G7YXHjqTF4THH7KYYxCBr4sM`
- Base 64: `OjGdDWujQOjP+m6PZSNrcQ`
- Base 91: `7M515sme(-[9YfN?/LIf`

## Encoded bits per character

The base values in this library have been chosen because they can encode an integral number of bits as either 1 or 2 characters, making the conversion relatively efficient since groups of bits can be directly converted using lookup arrays.

- Base 16: 4 bits per character
- Base 32: 5 bits per character
- Base 46: 5.5 bits per character (11 bits per 2 characters)
- Base 64: 6 bits per character
- Base 91: 6.5 bits per character (13 bits per 2 characters)

## Usage

Add the [NuGet package](https://www.nuget.org/packages/Zanaptak.BinaryToTextEncoding) to your project:
```
dotnet add package Zanaptak.BinaryToTextEncoding
```

### C#
```cs
using Zanaptak.BinaryToTextEncoding;

// Default codec
var originalBytes = new byte[] { 1, 2, 3 };
var encodedString = Base32.Default.Encode(originalBytes);
var decodedBytes = Base32.Default.Decode(encodedString);

// Custom character set
var customBase32 = new Base32("BCDFHJKMNPQRSTXZbcdfhjkmnpqrstxz");
var customOriginalBytes = new byte[] { 4, 5, 6 };
var customEncodedString = customBase32.Encode(customOriginalBytes);
var customDecodedBytes = customBase32.Decode(customEncodedString);

// Wrap output
var randomBytes = new byte[100];
new System.Random(12345).NextBytes(randomBytes);
Console.WriteLine(Base91.Default.Encode(randomBytes, 48));
//  Output:
//  r]g^oP{ZKd1>}lC{C*P){O96SL8z%0TW,4BfEof}%!b@a#:6
//  nN<c#=}80|srYHUy6$XP}4x945a~,ItFPS;U%a^<DMA]@m|#
//  12tC]*5+BoT-4Th,oVR9wvIv;Iym
```

### F#
```fs
open Zanaptak.BinaryToTextEncoding

// Default codec
let originalBytes = [| 1uy; 2uy; 3uy |]
let encodedString = Base32.Default.Encode originalBytes
let decodedBytes = Base32.Default.Decode encodedString

// Custom character set
let customBase32 = Base32("BCDFHJKMNPQRSTXZbcdfhjkmnpqrstxz")
let customOriginalBytes = [| 4uy; 5uy; 6uy |]
let customEncodedString = customBase32.Encode customOriginalBytes
let customDecodedBytes = customBase32.Decode customEncodedString

// Wrap output
let randomBytes = Array.create 100 0uy
System.Random(12345).NextBytes(randomBytes)
printfn "%s" (Base91.Default.Encode(randomBytes, 48))
//  Output:
//  r]g^oP{ZKd1>}lC{C*P){O96SL8z%0TW,4BfEof}%!b@a#:6
//  nN<c#=}80|srYHUy6$XP}4x945a~,ItFPS;U%a^<DMA]@m|#
//  12tC]*5+BoT-4Th,oVR9wvIv;Iym
```

## Order

If we think of the input byte array as a numeric value, the encoding is done in [big-endian](https://en.wikipedia.org/wiki/Endianness) order, starting with the most-significant bits of the most-significant byte. For inputs of the same length, if the character set is in ASCII order, then an ASCII string sort of the encoded outputs is the same as a numeric sort of the inputs.

Note however that some of default character sets used by different base values in this library are aligned with traditional implementations and not in ASCII order, so a custom character set should be used if sortability is required.

## Padding not supported

To reduce complexity, this library does not support padding. Padding does not affect encode/decode accuracy, only string length normalization. It is not needed when exact string lengths are known or otherwise delimited (such as quoted JSON strings). As such, it is left to the caller to handle externally if required, by trimming or appending padding as necessary.

## Legacy basE91 compatibility

This library provides two base 91 implementations: `Base91` and `Base91Legacy`. They are not compatible; the encoded output of one cannot be decoded by the other.

The main `Base91` algorithm works like the other `BaseXX` algorithms in the library; it encodes in big-endian order and constant-width (each 2-character pair encodes exactly 13 bits). The default character set is in ASCII order to preserve sortability of input, and excludes the characters `"`, `'`, and `\` to make it more easily quotable in programming languages.

`Base91Legacy` is based on the previously existing [basE91](http://base91.sourceforge.net/) algorithm. It encodes with a variable-width mechanism (some 2-character pairs can encode 14 bits instead of 13) which can result in slightly smaller encoded strings. Each two-character pair in the output is swapped compared to the main algorithm (least-significant char of the pair first), so sorting by string is not meaningful regardless of character set. Its default character set includes the `"` character, making it inconvenient to use in some programming languages and data formats such as JSON.


## Benchmarks

See the [benchmark project](https://github.com/zanaptak/BinaryToTextEncoding/tree/main/benchmark).

