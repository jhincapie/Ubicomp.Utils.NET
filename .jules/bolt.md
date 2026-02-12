## 2024-05-23 - Unexpected Performance Regression with EndPoint Caching
**Learning:** Caching `IPEndPoint` in `MulticastSocket` to avoid allocation/parsing on every send surprisingly degraded performance by ~20% in benchmarks. This might be due to contention on the shared `EndPoint` object within the underlying socket implementation when used with `BeginSendTo`, or overhead from APM pattern interacting with shared state.
**Action:** When optimizing `Socket` operations, benchmark carefully. Avoid assuming that object reuse is always faster, especially with legacy APM methods.

## 2024-05-23 - Global Serialization Locks
**Learning:** `TransportComponent` used static `importLock` and `exportLock` to synchronize serialization/deserialization. This creates a global bottleneck. Removing these locks improved throughput by ~5% in a concurrent benchmark and unlocks scalability.
**Action:** Avoid static locks for thread-safe operations like `JsonConvert.SerializeObject`.

## 2024-05-23 - Ordering Logic and Thread Safety
**Learning:** The `importLock` in `TransportComponent` was `static`, meaning it locked deserialization across all instances. However, message ordering (`EnforceOrdering`) is handled by a per-instance `gate` lock. Removing the static lock does not compromise ordering logic, even for out-of-order delivery, as validated by `TransportOrderingBenchmark`.
**Action:** Ensure logical ordering mechanisms (like buffering queues) are separate from processing safeguards (like serialization locks).

## 2024-05-24 - Zero-Allocation Send with ValueTask
**Learning:** Replacing `Task`-based APM socket methods with `ValueTask`-based `SendToAsync` allows passing `ReadOnlyMemory<byte>` directly, eliminating `byte[]` allocations and `Task` object overhead for every packet sent.
**Action:** Prefer `ValueTask` and `ReadOnlyMemory<byte>` over `byte[]` and `Task` for high-frequency network I/O methods.

## 2024-05-24 - Reflection Overhead in Hot Path
**Learning:** `Delegate.DynamicInvoke` used for message dispatch added significant overhead (~440ns per message) and allocations. Wrapping the typed handler in an `Action<object, MessageContext>` lambda allowed direct invocation, reducing dispatch cost to ~17ns (25x speedup) and eliminating allocations.
**Action:** Avoid `DynamicInvoke` in high-frequency loops; use delegate wrappers to bridge generic and specific types.

## 2024-05-24 - Timestamp Allocation in Hot Path
**Learning:** `TransportMessage.TimeStamp` property getter allocated a new string on every access, causing significant overhead in `DispatchMessage`. By storing raw ticks and lazily formatting only when needed, we reduced allocations by ~42% and execution time by ~19x in the dispatch path.
**Action:** Prefer raw primitive types (long ticks) for internal data flow and only convert to string for display/logging at the last moment or lazily.
