## 2024-05-23 - Unexpected Performance Regression with EndPoint Caching
**Learning:** Caching `IPEndPoint` in `MulticastSocket` to avoid allocation/parsing on every send surprisingly degraded performance by ~20% in benchmarks. This might be due to contention on the shared `EndPoint` object within the underlying socket implementation when used with `BeginSendTo`, or overhead from APM pattern interacting with shared state.
**Action:** When optimizing `Socket` operations, benchmark carefully. Avoid assuming that object reuse is always faster, especially with legacy APM methods.

## 2024-05-23 - Global Serialization Locks
**Learning:** `TransportComponent` used static `importLock` and `exportLock` to synchronize serialization/deserialization. This creates a global bottleneck. Removing these locks improved throughput by ~5% in a concurrent benchmark and unlocks scalability.
**Action:** Avoid static locks for thread-safe operations like `JsonConvert.SerializeObject`.

## 2024-05-23 - Ordering Logic and Thread Safety
**Learning:** The `importLock` in `TransportComponent` was `static`, meaning it locked deserialization across all instances. However, message ordering (`EnforceOrdering`) is handled by a per-instance `gate` lock. Removing the static lock does not compromise ordering logic, even for out-of-order delivery, as validated by `TransportOrderingBenchmark`.
**Action:** Ensure logical ordering mechanisms (like buffering queues) are separate from processing safeguards (like serialization locks).
