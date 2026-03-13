# ReplaceChainBenchmark.cs

This benchmark compares the performance of `string.Replace` chained `N` times versus using `StringWeaver.ReplaceAll` and `StringBuilder.Replace` for the same end result.

As mentioned in the introduction to the benchmarks, `string.Replace` creates `N` intermediate `string`s for a chain of `N` calls whereas `StringWeaver` mutates in-place and does not require intermediate allocations.

Sometimes, the growth strategy employed by `StringWeaver` may lead to additional allocations compared to `StringBuilder`, but in general (and the larger inputs and replacements get), `StringWeaver` will outperform both approaches.

```
| Method         | N   |        Mean | Ratio |   Gen0 | Allocated | Alloc Ratio |
| -------------- | --- | ----------: | ----: | -----: | --------: | ----------: |
| String_Replace | 1   |    31.33 ns |  1.00 | 0.0196 |     328 B |        1.00 |
| SW_ReplaceAll  | 1   |    85.20 ns |  2.72 | 0.0540 |     904 B |        2.76 |
| SB_Replace     | 1   |    54.62 ns |  1.75 | 0.0426 |     712 B |        2.17 |
|                |     |             |       |        |           |             |
| String_Replace | 5   |   157.18 ns |  1.00 | 0.0587 |     984 B |        1.00 |
| SW_ReplaceAll  | 5   |   300.17 ns |  1.91 | 0.0539 |     904 B |        0.92 |
| SB_Replace     | 5   |   169.92 ns |  1.08 | 0.0424 |     712 B |        0.72 |
|                |     |             |       |        |           |             |
| String_Replace | 10  |   282.63 ns |  1.00 | 0.0980 |    1640 B |        1.00 |
| SW_ReplaceAll  | 10  |   512.58 ns |  1.81 | 0.0539 |     904 B |        0.55 |
| SB_Replace     | 10  |   283.46 ns |  1.00 | 0.0424 |     712 B |        0.43 |
|                |     |             |       |        |           |             |
| String_Replace | 25  |   716.80 ns |  1.00 | 0.2604 |    4360 B |        1.00 |
| SW_ReplaceAll  | 25  | 1,221.22 ns |  1.71 | 0.0534 |     920 B |        0.21 |
| SB_Replace     | 25  |   985.68 ns |  1.38 | 0.0925 |    1560 B |        0.36 |
```

As you can see, `string.Replace` remains incredibly fast across all values of `N`, but its memory usage quickly balloons as `N` increases. `StringBuilder` already vastly outperforms it, but `StringWeaver` is more efficient still, which becomes more pronounced as `N` increases further. As far as execution time goes, `StringWeaver` will very likely never beat `string.Replace` and lag slightly behind `StringBuilder` for relatively small `N`. If and where you'll get savings depends on your specific use case, but in general, for larger `N`, `StringWeaver` will be the most efficient choice overall; run your own benchmarks on real data to be sure!