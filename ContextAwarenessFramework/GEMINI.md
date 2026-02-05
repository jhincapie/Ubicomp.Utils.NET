# ContextAwarenessFramework Context

## Architecture
The framework follows a **Monitor-Service-Entity** pattern.

*   **ContextMonitor**: The source of context. Runs on its own thread (if `Interval` or `Continuous`).
*   **ContextService**: The aggregator. Subscribes to Monitors.
*   **IEntity**: The data model.

## Threading Model
*   **Background Execution**: `ContextMonitor` instances typically run on their own threads (especially for `Interval` or `Continuous` types) to avoid blocking.
*   **Thread Safety**: `ContextService` uses internal locking (`lock`) to ensure safe state updates and persistence.
*   **UI Marshalling**: This framework is a pure .NET Standard library. It does **not** handle UI thread marshalling (e.g., `Dispatcher`). Consumers (WPF, MAUI apps) must handle marshalling when updating UI-bound collections from `ContextService` events.

## Persistence
`ContextService` includes a template method pattern for persistence:
*   `PersistenceType`: `None`, `Periodic`, `OnRequest`, `Combined`.
*   `ExecutePersit()`: Calls `PreparePersist()` then `PersistEntities()`.
*   Useful for logging context history or saving state.

## Logging
*   **Abstractions**: Uses `Microsoft.Extensions.Logging.Abstractions` to decouple from specific logging implementations.
*   **Injection**: Services expose a public `Logger` property that can be set by the host application.

## Key Classes
*   **`ContextMonitor`**: Abstract. Handles the run loop and event firing.
*   **`ContextService`**: Abstract. Handles threading, logging, and persistence.
*   **`ContextMonitorContainer` / `ContextServiceContainer`**: Used to manage collections of monitors/services and their lifecycles.

## Dependencies
*   **External**: `Microsoft.Extensions.Logging.Abstractions`