```

BenchmarkDotNet v0.13.12, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Processor 2.30GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX2


```
| Method                    | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| NewtonsoftSerialize       |  4.330 μs | 0.0543 μs | 0.0481 μs |  1.00 |    0.00 | 0.1602 |   3.84 KB |        1.00 |
| SystemTextJsonSerialize   |  2.112 μs | 0.0395 μs | 0.0350 μs |  0.49 |    0.01 | 0.0534 |   1.24 KB |        0.32 |
| NewtonsoftDeserialize     | 19.424 μs | 0.3114 μs | 0.2913 μs |  4.49 |    0.08 | 0.4883 |   11.7 KB |        3.05 |
| SystemTextJsonDeserialize | 10.085 μs | 0.0399 μs | 0.0333 μs |  2.33 |    0.03 | 0.0763 |   2.05 KB |        0.53 |
