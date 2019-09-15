# Benchmarks

## Environment

``` ini
BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
Intel Core i7-4790K CPU 4.00GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.2.402
  [Host]     : .NET Core 2.2.7 (CoreCLR 4.6.28008.02, CoreFX 4.6.28008.03), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.2.7 (CoreCLR 4.6.28008.02, CoreFX 4.6.28008.03), 64bit RyuJIT
```

## Encoding 16 bytes

|                     Method |     Mean |     Error |    StdDev |
|--------------------------- |---------:|----------:|----------:|
|            NetBase64Encode | 30.90 ns | 0.0694 ns | 0.0649 ns |
|       ZanaptakBase16Encode | 56.79 ns | 0.2988 ns | 0.2332 ns |
|       ZanaptakBase32Encode | 65.80 ns | 0.5370 ns | 0.4760 ns |
|       ZanaptakBase46Encode | 67.07 ns | 0.0848 ns | 0.0752 ns |
|       ZanaptakBase64Encode | 62.19 ns | 0.4956 ns | 0.4636 ns |
|       ZanaptakBase91Encode | 62.25 ns | 0.0996 ns | 0.0883 ns |
| ZanaptakBase91LegacyEncode | 63.08 ns | 0.2475 ns | 0.2315 ns |

## Decoding 16 bytes

|                     Method |     Mean |     Error |    StdDev |
|--------------------------- |---------:|----------:|----------:|
|            NetBase64Decode | 66.31 ns | 0.0844 ns | 0.0748 ns |
|       ZanaptakBase16Decode | 84.94 ns | 0.4920 ns | 0.4603 ns |
|       ZanaptakBase32Decode | 78.68 ns | 0.1718 ns | 0.1523 ns |
|       ZanaptakBase46Decode | 78.52 ns | 0.5120 ns | 0.4539 ns |
|       ZanaptakBase64Decode | 65.45 ns | 0.1947 ns | 0.1726 ns |
|       ZanaptakBase91Decode | 68.39 ns | 0.4434 ns | 0.4147 ns |
| ZanaptakBase91LegacyDecode | 88.62 ns | 0.4311 ns | 0.3600 ns |

## Encoding 999 bytes

|                     Method |     Mean |     Error |    StdDev |
|--------------------------- |---------:|----------:|----------:|
|            NetBase64Encode | 1.085 us | 0.0102 us | 0.0096 us |
|       ZanaptakBase16Encode | 2.409 us | 0.0275 us | 0.0257 us |
|       ZanaptakBase32Encode | 2.765 us | 0.0198 us | 0.0185 us |
|       ZanaptakBase46Encode | 3.022 us | 0.0341 us | 0.0319 us |
|       ZanaptakBase64Encode | 2.570 us | 0.0206 us | 0.0183 us |
|       ZanaptakBase91Encode | 2.729 us | 0.0084 us | 0.0074 us |
| ZanaptakBase91LegacyEncode | 2.758 us | 0.0216 us | 0.0202 us |

## Decoding 999 bytes

|                     Method |     Mean |     Error |    StdDev |
|--------------------------- |---------:|----------:|----------:|
|            NetBase64Decode | 1.931 us | 0.0104 us | 0.0092 us |
|       ZanaptakBase16Decode | 3.543 us | 0.0180 us | 0.0150 us |
|       ZanaptakBase32Decode | 3.663 us | 0.0371 us | 0.0347 us |
|       ZanaptakBase46Decode | 3.863 us | 0.0107 us | 0.0089 us |
|       ZanaptakBase64Decode | 3.185 us | 0.0082 us | 0.0073 us |
|       ZanaptakBase91Decode | 3.370 us | 0.0206 us | 0.0172 us |
| ZanaptakBase91LegacyDecode | 4.149 us | 0.0779 us | 0.0729 us |
