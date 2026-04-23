```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.0.1 (25A362) [Darwin 25.0.0]
Apple M4 Max, 1 CPU, 16 logical and 16 physical cores
.NET SDK 10.0.202
  [Host]     : .NET 10.0.6 (10.0.6, 10.0.626.17701), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.6 (10.0.6, 10.0.626.17701), Arm64 RyuJIT armv8.0-a


```
| Method              | Mean      | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|-------------------- |----------:|---------:|---------:|------:|----------:|------------:|
| Swiss_Identity      |  78.61 μs | 0.200 μs | 0.178 μs |  1.00 |         - |          NA |
| Swiss_Lowbias32     | 132.25 μs | 0.531 μs | 0.471 μs |  1.68 |         - |          NA |
| Swiss_Fibonacci     | 108.77 μs | 0.491 μs | 0.459 μs |  1.38 |         - |          NA |
| Swiss_Xmx           | 132.70 μs | 0.547 μs | 0.512 μs |  1.69 |         - |          NA |
| RobinHood_Identity  |  49.73 μs | 0.183 μs | 0.153 μs |  0.63 |         - |          NA |
| RobinHood_Fibonacci |  85.02 μs | 0.367 μs | 0.325 μs |  1.08 |         - |          NA |
