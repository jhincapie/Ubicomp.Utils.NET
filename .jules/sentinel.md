## 2026-02-02 - [Swallowed Exceptions in Background Threads]
**Vulnerability:** Found `catch {}` block in `ThreadedNotifyMulticastSocketListener` which swallowed all exceptions in a background thread.
**Learning:** Legacy networking code often prioritizes "staying alive" over correctness, hiding critical failures.
**Prevention:** Always log exceptions in background threads (`Console.Error` or logger) even if recovery is not possible, to ensure visibility.

## 2026-02-02 - [Busy Wait in Legacy Synchronization]
**Vulnerability:** Found a busy-wait loop in `TransportComponent` where an `EventWaitHandle` was checked in a loop without proper waiting, leading to potential DoS (100% CPU).
**Learning:** Legacy code attempting to implement custom "turn-based" locking often gets the synchronization primitives wrong (e.g. using `ManualResetEvent` incorrectly).
**Prevention:** Replace custom spinning/waiting logic with standard `Monitor.Wait`/`Monitor.Pulse` patterns or `SemaphoreSlim` to ensure OS-level blocking.
