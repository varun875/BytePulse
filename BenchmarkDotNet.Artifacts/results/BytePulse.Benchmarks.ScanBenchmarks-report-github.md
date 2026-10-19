```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i5-13450HX 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-AMZPBM : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                                     | Job        | IterationCount | LaunchCount | WarmupCount | FileCount | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------------------------- |----------- |--------------- |------------ |------------ |---------- |------------:|-------------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| &#39;naive: framework enumeration, one thread&#39; | Job-AMZPBM | 5              | Default     | 2           | 20000     |   590.69 ms |    94.704 ms |  24.594 ms |  1.00 |    0.05 |        - |        - |        - | 5656.56 KB |        1.00 |
| &#39;naive: native enumeration, one thread&#39;    | Job-AMZPBM | 5              | Default     | 2           | 20000     |    19.58 ms |     6.422 ms |   1.668 ms |  0.03 |    0.00 |  31.2500 |        - |        - |  496.67 KB |        0.09 |
| &#39;parallel scanner, one thread&#39;             | Job-AMZPBM | 5              | Default     | 2           | 20000     |    67.48 ms |    10.602 ms |   2.753 ms |  0.11 |    0.01 | 700.0000 | 700.0000 | 700.0000 | 4268.29 KB |        0.75 |
| &#39;parallel scanner, four threads&#39;           | Job-AMZPBM | 5              | Default     | 2           | 20000     |    46.51 ms |     0.817 ms |   0.212 ms |  0.08 |    0.00 | 636.3636 | 636.3636 | 636.3636 | 4272.44 KB |        0.76 |
| &#39;parallel scanner, eight threads&#39;          | Job-AMZPBM | 5              | Default     | 2           | 20000     |    46.47 ms |     1.624 ms |   0.251 ms |  0.08 |    0.00 | 636.3636 | 636.3636 | 636.3636 | 4273.63 KB |        0.76 |
|                                            |            |                |             |             |           |             |              |            |       |         |          |          |          |            |             |
| &#39;naive: framework enumeration, one thread&#39; | ShortRun   | 3              | 1           | 3           | 20000     | 1,316.91 ms | 3,288.325 ms | 180.244 ms |  1.01 |    0.18 |        - |        - |        - | 5656.56 KB |        1.00 |
| &#39;naive: native enumeration, one thread&#39;    | ShortRun   | 3              | 1           | 3           | 20000     |    36.45 ms |    30.262 ms |   1.659 ms |  0.03 |    0.00 |  31.2500 |        - |        - |  496.67 KB |        0.09 |
| &#39;parallel scanner, one thread&#39;             | ShortRun   | 3              | 1           | 3           | 20000     |    63.05 ms |    43.683 ms |   2.394 ms |  0.05 |    0.01 | 700.0000 | 700.0000 | 700.0000 | 4268.68 KB |        0.75 |
| &#39;parallel scanner, four threads&#39;           | ShortRun   | 3              | 1           | 3           | 20000     |    46.61 ms |     4.517 ms |   0.248 ms |  0.04 |    0.00 | 666.6667 | 666.6667 | 666.6667 | 4273.02 KB |        0.76 |
| &#39;parallel scanner, eight threads&#39;          | ShortRun   | 3              | 1           | 3           | 20000     |    47.54 ms |    23.225 ms |   1.273 ms |  0.04 |    0.00 | 666.6667 | 666.6667 | 666.6667 | 4272.34 KB |        0.76 |
