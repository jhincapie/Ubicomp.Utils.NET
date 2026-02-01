## 2025-02-17 - Swallowed Exceptions in Thread Pool
**Vulnerability:** Application swallowed exceptions occurring in background threads (`ThreadedNotifyMulticastSocketListener`), masking errors and potential security failures in listeners.
**Learning:** `ThreadPool.QueueUserWorkItem` callbacks must handle their own exceptions. Using an empty `catch {}` block to prevent crashes hides critical application state and security failures.
**Prevention:** Always wrap thread pool callbacks in try-catch blocks that log the exception (fail securely) or notify a centralized error handler.
