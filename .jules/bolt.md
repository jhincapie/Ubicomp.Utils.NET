## 2024-05-23 - Unexpected Performance Regression with EndPoint Caching
**Learning:** Caching `IPEndPoint` in `MulticastSocket` to avoid allocation/parsing on every send surprisingly degraded performance by ~20% in benchmarks. This might be due to contention on the shared `EndPoint` object within the underlying socket implementation when used with `BeginSendTo`, or overhead from APM pattern interacting with shared state.
**Action:** When optimizing `Socket` operations, benchmark carefully. Avoid assuming that object reuse is always faster, especially with legacy APM methods.

## 2024-05-23 - Global Serialization Locks
**Learning:** `TransportComponent` used static `importLock` and `exportLock` to synchronize serialization/deserialization. This creates a global bottleneck across all `TransportComponent` instances and limits concurrency. Removing these locks improved throughput by ~5% in a concurrent benchmark and unlocks scalability.
**Action:** Avoid static locks for thread-safe operations like `JsonConvert.SerializeObject`.
