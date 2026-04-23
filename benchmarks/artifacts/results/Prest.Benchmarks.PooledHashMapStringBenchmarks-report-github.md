```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.0.1 (25A362) [Darwin 25.0.0]
Apple M4 Max, 1 CPU, 16 logical and 16 physical cores
.NET SDK 10.0.202
  [Host]     : .NET 10.0.6 (10.0.6, 10.0.626.17701), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.6 (10.0.6, 10.0.626.17701), Arm64 RyuJIT armv8.0-a


```
| Method                             | N     | Mean           | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------------------- |------ |---------------:|-------------:|-------------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **&#39;Dictionary.TryGetValue (hit)&#39;**     | **256**   |       **712.3 ns** |      **5.05 ns** |      **4.72 ns** |  **1.00** |    **0.01** |        **-** |        **-** |        **-** |         **-** |          **NA** |
| &#39;PooledHashMap.TryGetValue (hit)&#39;  | 256   |     1,102.7 ns |      7.10 ns |      6.29 ns |  1.55 |    0.01 |        - |        - |        - |         - |          NA |
| &#39;DenseMap.Get (hit)&#39;               | 256   |     1,046.7 ns |      7.55 ns |      6.31 ns |  1.47 |    0.01 |        - |        - |        - |         - |          NA |
| &#39;Dictionary.TryGetValue (miss)&#39;    | 256   |       687.9 ns |      5.53 ns |      4.90 ns |  0.97 |    0.01 |        - |        - |        - |         - |          NA |
| &#39;PooledHashMap.TryGetValue (miss)&#39; | 256   |     1,198.8 ns |      6.10 ns |      5.70 ns |  1.68 |    0.01 |        - |        - |        - |         - |          NA |
| &#39;DenseMap.Get (miss)&#39;              | 256   |     1,176.2 ns |      6.41 ns |      5.35 ns |  1.65 |    0.01 |        - |        - |        - |         - |          NA |
| &#39;Dictionary.Add (from empty)&#39;      | 256   |     1,492.1 ns |     24.64 ns |     20.57 ns |  2.09 |    0.03 |   0.9956 |   0.0286 |        - |    8336 B |          NA |
| &#39;PooledHashMap.Add (from empty)&#39;   | 256   |     1,829.2 ns |     15.13 ns |     14.16 ns |  2.57 |    0.03 |        - |        - |        - |         - |          NA |
| &#39;DenseMap.Insert (from empty)&#39;     | 256   |     4,927.7 ns |     16.91 ns |     14.12 ns |  6.92 |    0.05 |   1.6098 |   0.0534 |        - |   13520 B |          NA |
|                                    |       |                |              |              |       |         |          |          |          |           |             |
| **&#39;Dictionary.TryGetValue (hit)&#39;**     | **4096**  |    **14,142.7 ns** |     **47.52 ns** |     **39.68 ns** |  **1.00** |    **0.00** |        **-** |        **-** |        **-** |         **-** |          **NA** |
| &#39;PooledHashMap.TryGetValue (hit)&#39;  | 4096  |    19,910.6 ns |     87.79 ns |     77.82 ns |  1.41 |    0.01 |        - |        - |        - |         - |          NA |
| &#39;DenseMap.Get (hit)&#39;               | 4096  |    19,656.0 ns |     82.08 ns |     72.76 ns |  1.39 |    0.01 |        - |        - |        - |         - |          NA |
| &#39;Dictionary.TryGetValue (miss)&#39;    | 4096  |    13,623.6 ns |     57.77 ns |     45.10 ns |  0.96 |    0.00 |        - |        - |        - |         - |          NA |
| &#39;PooledHashMap.TryGetValue (miss)&#39; | 4096  |    21,475.4 ns |    177.83 ns |    157.64 ns |  1.52 |    0.01 |        - |        - |        - |         - |          NA |
| &#39;DenseMap.Get (miss)&#39;              | 4096  |    21,394.9 ns |    212.34 ns |    188.23 ns |  1.51 |    0.01 |        - |        - |        - |         - |          NA |
| &#39;Dictionary.Add (from empty)&#39;      | 4096  |    42,649.4 ns |    696.95 ns |    617.83 ns |  3.02 |    0.04 |  36.9873 |  36.9873 |  36.9873 |  136185 B |          NA |
| &#39;PooledHashMap.Add (from empty)&#39;   | 4096  |    29,404.6 ns |    128.78 ns |    114.16 ns |  2.08 |    0.01 |        - |        - |        - |         - |          NA |
| &#39;DenseMap.Insert (from empty)&#39;     | 4096  |    91,302.9 ns |    518.82 ns |    459.92 ns |  6.46 |    0.04 |  41.6260 |  41.6260 |  41.6260 |  209388 B |          NA |
|                                    |       |                |              |              |       |         |          |          |          |           |             |
| **&#39;Dictionary.TryGetValue (hit)&#39;**     | **65536** |   **291,571.0 ns** |  **5,705.73 ns** |  **8,883.13 ns** |  **1.00** |    **0.04** |        **-** |        **-** |        **-** |         **-** |          **NA** |
| &#39;PooledHashMap.TryGetValue (hit)&#39;  | 65536 |   400,274.9 ns |  7,844.80 ns | 10,200.46 ns |  1.37 |    0.05 |        - |        - |        - |         - |          NA |
| &#39;DenseMap.Get (hit)&#39;               | 65536 |   415,252.9 ns |  8,304.31 ns |  8,155.94 ns |  1.43 |    0.05 |        - |        - |        - |         - |          NA |
| &#39;Dictionary.TryGetValue (miss)&#39;    | 65536 |   671,256.6 ns | 13,372.39 ns | 29,070.48 ns |  2.30 |    0.12 |        - |        - |        - |         - |          NA |
| &#39;PooledHashMap.TryGetValue (miss)&#39; | 65536 |   446,538.0 ns |  6,231.54 ns |  5,524.09 ns |  1.53 |    0.05 |        - |        - |        - |         - |          NA |
| &#39;DenseMap.Get (miss)&#39;              | 65536 |   454,123.6 ns |  3,804.20 ns |  3,558.45 ns |  1.56 |    0.05 |        - |        - |        - |         - |          NA |
| &#39;Dictionary.Add (from empty)&#39;      | 65536 |   726,707.9 ns | 14,249.10 ns | 24,579.00 ns |  2.49 |    0.11 |  90.8203 |  90.8203 |  90.8203 | 2112904 B |          NA |
| &#39;PooledHashMap.Add (from empty)&#39;   | 65536 |   596,632.7 ns |  3,697.63 ns |  3,277.85 ns |  2.05 |    0.06 |        - |        - |        - |         - |          NA |
| &#39;DenseMap.Insert (from empty)&#39;     | 65536 | 1,818,548.9 ns | 12,247.72 ns | 10,227.40 ns |  6.24 |    0.19 | 162.1094 | 154.2969 | 154.2969 | 3344022 B |          NA |
