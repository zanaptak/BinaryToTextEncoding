# BinaryToTextEncoding

A binary-to-text encoder/decoder library for .NET and Fable. Provides base 16, base 32, base 46, base 64, and base 91 codecs. Supports custom character sets.

## Output example

Example of a random 16-byte array (same size as a GUID) encoded in each base:

* Base 16: `3A319D0D6BA340E8CFFA6E8F65236B71`
* Base 32: `HIYZ2DLLUNAORT72N2HWKI3LOE`
* Base 46: `G7YXHjqTF4THH7KYYxCBr4sM`
* Base 64: `OjGdDWujQOjP+m6PZSNrcQ`
* Base 91: `7M515sme(-[9YfN?/LIf`

## Encoded bits per character

The base values in this library have been chosen because they can encode an integral number of bits as either 1 or 2 characters, making the conversion relatively efficient since groups of bits can be directly converted using lookup arrays.

* Base 16: 4 bits per character
* Base 32: 5 bits per character
* Base 46: 5.5 bits per character (11 bits per 2 characters)
* Base 64: 6 bits per character
* Base 91: 6.5 bits per character (13 bits per 2 characters)


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

## Padding

To reduce complexity, this library does not support padding. (Padding does not affect encode/decode accuracy, only string length normalization.) To decode padded data from another source, call `.TrimEnd('=')` on the string before sending to the decoder.

## Base 91 compatibility

The default base 91 codec in this library is incompatible with the prior `basE91` algorithm [found here](http://base91.sourceforge.net/). That algorithm encodes in little-endian order and has a variable-width mechanism (some 2-character pairs can encode 14 bits instead of 13). Its output does not preserve sortability of the input, and its character set includes the `"` character, making it inconvenient to use in some programming languages and data formats such as JSON.

The updated `Base91` algorithm in this library processes bytes in big-endian order with constant-width encoding, for consistency with the other base value codecs and to support sorting. It also uses a modified character set by default that excludes the characters `"`, `'`, and `\`, making it more easily quotable in programming languages.

This library does include an implementation of that prior `basE91` algorithm under the name `Base91Legacy`.

## Benchmarks

See the [benchmark project](https://github.com/zanaptak/BinaryToTextEncoding/tree/master/benchmark).

