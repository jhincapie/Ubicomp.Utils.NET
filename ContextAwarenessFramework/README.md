# ContextAwarenessFramework

A framework for building context-aware applications by decoupling context sensing (`ContextMonitor`) from context management and application logic (`ContextService`).

## Overview
The **ContextAwarenessFramework** provides a standard structure for:
1.  **Sensing**: Acquiring data from sensors or software events via `ContextMonitor`.
2.  **Processing**: Aggregating and transforming raw data via `ContextService`.
3.  **Data Modeling**: Representing context using `IEntity` objects.
4.  **Notification**: Thread-safe updates via observer pattern.

## Key Components

### ContextMonitor
Abstract base class for data producers.
- **Update Types**: Continuous, Interval, or OnRequest.
- **Events**: Fires `OnNotifyContextServices` when new data is available.
- **Usage**: Inherit from this to create specific sensors (e.g., `LocationMonitor`, `BatteryMonitor`).

### ContextService
Abstract base class for data consumers/managers.
- **Implements**: `IContextMonitorListener`.
- **Threading**: Executes updates on the calling monitor's thread. Users can implement their own marshaling if UI updates are needed.
- **Persistence**: Built-in support for periodic or request-based data saving (`PersistEntities`).
- **Logging**: Exposes a `Logger` property (defaults to `NullLogger`) for injecting `Microsoft.Extensions.Logging` implementations.
- **Usage**: Inherit from this to implement logic (e.g., `LocationService` that aggregates GPS and WiFi data).

### IEntity
Interface for data objects.
- **Implements**: `INotifyPropertyChanged` for data binding support.
- **Usage**: Define your domain models (e.g., `UserLocation`) implementing this interface.

## Usage Example
... (rest of usage) ...

## Dependencies
- `Microsoft.Extensions.Logging.Abstractions`