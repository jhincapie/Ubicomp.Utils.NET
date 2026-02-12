```

BenchmarkDotNet v0.13.12, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Processor 2.30GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.100
  [Host]   : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0

```
| Method                  | Mean      | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|----------:|----------:|------:|-------:|----------:|------------:|
| CreateContext_Legacy    | 559.92 ns | 11.024 ns | 10.827 ns |  1.00 | 0.0057 |     152 B |        1.00 |
| CreateContext_Optimized |  29.50 ns |  0.566 ns |  0.502 ns |  0.05 | 0.0037 |      88 B |        0.58 |
