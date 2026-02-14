# Sentinel's Journal

## 2024-05-24 - Unbounded Replay Cache DoS
**Vulnerability:** The `TransportComponent` used a `ConcurrentDictionary<Guid, DateTime>` to track seen messages for replay protection. This dictionary was unbounded, meaning an attacker could flood the system with messages having unique `MessageId`s, causing the dictionary to grow until memory exhaustion (DoS).
**Learning:** Replay protection mechanisms that store state must always have an upper bound. Relying solely on time-based expiration is insufficient if the rate of incoming unique items exceeds the cleanup rate or available memory.
**Prevention:** Implemented a `MaxReplayCacheSize` limit (default 100,000) and tracked the count using `Interlocked` operations. When the limit is reached, the system fails securely by dropping new messages and logging a warning.
