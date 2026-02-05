# ContextAwarenessFramework Context

## Architecture
The framework follows the **Monitor-Service-Entity (MSE)** pattern to separate concerns in context-aware applications.

![MSE Pattern Diagram](assets/mse_pattern_diagram.png)

## Core Components

### 1. Monitor (ContextMonitor)
*   **Role**: The source of truth. Acquires raw data from hardware, sensors, or APIs.
*   **Behavior**: Can be active (own thread/timer) or passive. Fires events when data changes.

### 2. Service (ContextService)
*   **Role**: The coordinator.
*   **Responsibilities**:
    *   Subscribes to Monitors.
    *   Applies business logic/aggregation.
    *   Updates the **Entity**.
    *   Handles **Persistence** (saving history).
    *   Marshals updates to the UI thread (via `Dispatcher`).

### 3. Entity (IEntity)
*   **Role**: The data model.
*   **Behavior**: Passive POCOs that implement `INotifyPropertyChanged`. Designed to be bound directly to UI (WPF/MAUI) or serialized for transport.

## Threading Model
*   **Isolation**: Monitors typically run on background threads to avoid blocking the UI.
*   **Marshalling**: `ContextService` captures the `Dispatcher` at creation. All updates to the `Entity` (and thus the UI) are automatically marshalled to the correct thread.

## Persistence
*   **Pattern**: Template Method (`ExecutePersist`, `PersistEntities`).
*   **Modes**: `Periodic`, `OnRequest`, `Combined`.
