I've decided to add some benchmarks to demonstrate the performance characteristics of `StringWeaver` compared to common bad code patterns like chained `string.Replace` calls.

# Introduction

It should be noted first and foremost that for literal replacements with short seed `string`s passed into `string.Replace` will often vastly outperform `StringWeaver`, both in execution time and memory usage (at the cost of polluting your application's memory with string allocations). `StringWeaver`'s impact is more pronounced when dealing with larger inputs, many replacements, or replacements determined at runtime (or anything involving regex).

Each file in this wiki directory will cover a corresponding benchmark class. For its code, refer directly to the .cs file.

# Disclaimer

As Stephen Toub always mentions in his insanely good performance articles (such as https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/#benchmarking-setup):

> [T]hese are micro-benchmarks, timing operations so short you'd miss them by blinking (but when such operations run millions of times, the savings really add up). The exact numbers you get will depend on your hardware, your operating system, what else your machine is juggling at the moment, how much coffee you've had since breakfast, and perhaps whether Mercury is in retrograde. In other words, don't expect your results to match mine exactly, but I've picked tests that should still be reasonably reproducible in the real world.

My benchmark results were produced by runs on the following setup:
```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.7523)
AMD Ryzen 9 7900X 4.70GHz, 1 CPU, 24 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]   : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v4

// HardwareIntrinsics=AVX512 BITALG+VBMI2+VNNI+VPOPCNTDQ,AVX512 IFMA+VBMI,AVX512 F+BW+CD+DQ+VL,AVX2+BMI1+BMI2+F16C+FMA+LZCNT+MOVBE,AVX,SSE3+SSSE3+SSE4.1+SSE4.2+POPCNT,X86Base+SSE+SSE2,AES+PCLMUL VectorSize=256
```