```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.0.1 (25A362) [Darwin 25.0.0]
Apple M4 Max, 1 CPU, 16 logical and 16 physical cores
.NET SDK 10.0.202
  [Host]     : .NET 10.0.6 (10.0.6, 10.0.626.17701), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.6 (10.0.6, 10.0.626.17701), Arm64 RyuJIT armv8.0-a


```
| Method            | N     | Mean        | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------------ |------ |------------:|----------:|----------:|------:|----------:|------------:|
| **Dictionary_Lookup** | **256**   |    **324.9 ns** |   **2.12 ns** |   **1.99 ns** |  **1.00** |         **-** |          **NA** |
| Swiss_Lookup      | 256   |    303.8 ns |   1.69 ns |   1.58 ns |  0.94 |         - |          NA |
| Linear_Lookup     | 256   |    203.0 ns |   1.31 ns |   1.22 ns |  0.62 |         - |          NA |
| RobinHood_Lookup  | 256   |    191.0 ns |   0.94 ns |   0.83 ns |  0.59 |         - |          NA |
| Chained_Lookup    | 256   |    212.7 ns |   1.11 ns |   1.04 ns |  0.65 |         - |          NA |
|                   |       |             |           |           |       |           |             |
| **Dictionary_Lookup** | **4096**  |  **4,967.7 ns** |  **40.21 ns** |  **35.64 ns** |  **1.00** |         **-** |          **NA** |
| Swiss_Lookup      | 4096  |  4,740.5 ns |  42.63 ns |  39.87 ns |  0.95 |         - |          NA |
| Linear_Lookup     | 4096  |  3,202.3 ns |  11.37 ns |  10.64 ns |  0.64 |         - |          NA |
| RobinHood_Lookup  | 4096  |  3,058.3 ns |  12.99 ns |  12.15 ns |  0.62 |         - |          NA |
| Chained_Lookup    | 4096  |  3,388.9 ns |  18.27 ns |  16.19 ns |  0.68 |         - |          NA |
|                   |       |             |           |           |       |           |             |
| **Dictionary_Lookup** | **65536** | **80,369.9 ns** | **475.66 ns** | **421.66 ns** |  **1.00** |         **-** |          **NA** |
| Swiss_Lookup      | 65536 | 76,884.3 ns | 359.14 ns | 299.90 ns |  0.96 |         - |          NA |
| Linear_Lookup     | 65536 | 51,711.6 ns | 178.65 ns | 167.11 ns |  0.64 |         - |          NA |
| RobinHood_Lookup  | 65536 | 50,616.4 ns | 235.04 ns | 219.85 ns |  0.63 |         - |          NA |
| Chained_Lookup    | 65536 | 56,141.2 ns | 538.58 ns | 503.79 ns |  0.70 |         - |          NA |
