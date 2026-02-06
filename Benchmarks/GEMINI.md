# Benchmarks Context

## Purpose
Performance verification using `BenchmarkDotNet`.

## Key Benchmarks
*   **Serialization**: `Newtonsoft.Json` vs `System.Text.Json`.
    *   *Result*: `System.Text.Json` is ~2x faster and is the default for the framework.
*   **Transport**: Measuring throughput of `TransportComponent` message processing.

## Usage
Run in `Release` mode to get accurate results.

## Do's and Don'ts
*   **Do** run benchmarks in `Release` configuration.
*   **Do** use these baselines when proposing performance-critical changes.
*   **Don't** rely on Debug build metrics for decision making.
