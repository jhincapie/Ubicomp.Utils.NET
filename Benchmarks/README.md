# Benchmarks

This project contains performance benchmarks for the Ubicomp.Utils.NET solution, utilizing **BenchmarkDotNet**.

## Scenarios
*   **Serialization**: Compares the performance of `Newtonsoft.Json` (Legacy) vs `System.Text.Json` (Modern) for `TransportMessage` serialization and polymorphic deserialization.

## Running Benchmarks
Benchmarks should always be run in **Release** configuration to ensure accurate results.

```bash
dotnet run -c Release --project Benchmarks/Benchmarks.csproj
```

## Results
Recent runs demonstrate that `System.Text.Json` provides approximately **2x performance improvement** and reduced memory allocations compared to `Newtonsoft.Json`.
