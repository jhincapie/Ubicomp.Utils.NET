# ContextAwarenessFramework Context

## Module Purpose
**ContextAwarenessFramework** provides the infrastructure for context-aware applications using the **Monitor-Service-Entity (MSE)** pattern.

## Architectural Context
*   **Layer**: Top layer (Application Logic & Context Management).
*   **Used By**: Applications (e.g., Smart Home, IoT Dashboards).
*   **Dependencies**: `MulticastTransportFramework` (optional integration).

## Key Components

### 1. ContextMonitor
*   **Role**: Source of truth (Producer).
*   **Lifecycle**: Managed by `ContextMonitorContainer`.
*   **Threading**: Updates usually originate on background threads.
*   **Events**: `OnNotifyContextServices` (standard .NET event).

### 2. ContextService
*   **Role**: Logic & Persistence (Consumer/Coordinator).
*   **Lifecycle**: Managed by `ContextServiceContainer`.
*   **Threading**: Does **not** automatically marshal to UI thread.
*   **Persistence**: Template methods for saving state (`ExecutePersist`).

### 3. IEntity
*   **Role**: Data Model.
*   **Contract**: `INotifyPropertyChanged`.
*   **Serialization**: Designed to be serialized (JSON/Binary) for transport.

## Do's and Don'ts

### Do's
*   **Do** use `ContextMonitorContainer` and `ContextServiceContainer` for lifecycle management.
*   **Do** keep `IEntity` classes simple (POCOs) for easier serialization.
*   **Do** handle UI thread marshalling manually in your Service or View layer.

### Don'ts
*   **Don't** perform heavy blocking operations in `UpdateMonitorReading` (it blocks the monitor thread).
*   **Don't** put business logic in `IEntity` classes.

## File Structure
*   `ContextAdapter/ContextMonitor.cs`: Base class for monitors.
*   `ContextAdapter/ContextMonitorContainer.cs`: Lifecycle manager for monitors.
*   `ContextService/ContextService.cs`: Base class for services.
*   `ContextService/ContextServiceContainer.cs`: Lifecycle manager for services.
*   `DataModel/IEntity.cs`: Interface for data entities.
