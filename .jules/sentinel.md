## 2026-02-02 - [Swallowed Exceptions in Background Threads]
**Vulnerability:** Found `catch {}` block in `ThreadedNotifyMulticastSocketListener` which swallowed all exceptions in a background thread.
**Learning:** Legacy networking code often prioritizes "staying alive" over correctness, hiding critical failures.
**Prevention:** Always log exceptions in background threads (`Console.Error` or logger) even if recovery is not possible, to ensure visibility.
