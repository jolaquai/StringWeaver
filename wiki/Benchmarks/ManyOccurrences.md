# ManyOccurrencesBenchmark.cs

This benchmark compares the performance of `string.Replace` chained `N` times versus using `StringWeaver.ReplaceAll` and `StringBuilder.Replace` for the same end result, specifically when the input string consists of many occurrences of the substring being replaced. Specifically closes in on the case where many copies are required to make space for the longer replacement substring.

```
| Method         | N     |         Mean | Ratio |    Gen0 |    Gen1 |    Gen2 | Allocated | Alloc Ratio |
| -------------- | ----- | -----------: | ----: | ------: | ------: | ------: | --------: | ----------: |
| String_Replace | 10    |     119.4 ns |  1.00 |  0.0072 |       - |       - |     120 B |        1.00 |
| SW_ReplaceAll  | 10    |     318.3 ns |  2.67 |  0.0415 |       - |       - |     696 B |        5.80 |
| SB_Replace     | 10    |     158.5 ns |  1.33 |  0.0224 |       - |       - |     376 B |        3.13 |
|                |       |              |       |         |         |         |           |             |
| String_Replace | 100   |   1,309.1 ns |  1.00 |  0.0610 |       - |       - |    1024 B |        1.00 |
| SW_ReplaceAll  | 100   |   2,670.8 ns |  2.04 |  0.1755 |       - |       - |    2936 B |        2.87 |
| SB_Replace     | 100   |   1,542.2 ns |  1.18 |  0.1278 |       - |       - |    2168 B |        2.12 |
|                |       |              |       |         |         |         |           |             |
| String_Replace | 1000  |  11,765.3 ns |  1.00 |  0.5951 |       - |       - |   10024 B |        1.00 |
| SW_ReplaceAll  | 1000  |  27,462.8 ns |  2.33 |  2.0447 |  0.1221 |       - |   34496 B |        3.44 |
| SB_Replace     | 1000  |  14,354.8 ns |  1.22 |  1.2054 |  0.0458 |       - |   20168 B |        2.01 |
|                |       |              |       |         |         |         |           |             |
| String_Replace | 10000 | 152,009.3 ns |  1.00 | 31.0059 | 31.0059 | 31.0059 |  100045 B |        1.00 |
| SW_ReplaceAll  | 10000 | 302,120.9 ns |  1.99 | 71.2891 | 71.2891 | 71.2891 |  311208 B |        3.11 |
| SB_Replace     | 10000 | 169,580.3 ns |  1.12 | 31.0059 | 31.0059 | 31.0059 |  200189 B |        2.00 |
```
```
