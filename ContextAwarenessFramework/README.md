# ContextAwarenessFramework

A framework for building context-aware applications by decoupling context sensing (`ContextMonitor`) from context management and application logic (`ContextService`) using the **Monitor-Service-Entity (MSE)** pattern.

## Overview
The **ContextAwarenessFramework** provides a standard structure for:
1.  **Sensing**: Acquiring data from sensors or external events via `ContextMonitor`.
2.  **Processing**: Aggregating, transforming, and persisting context via `ContextService`.
3.  **Data Modeling**: Representing state using thread-safe `IEntity` objects.
4.  **Notification**: Seamless UI binding support via `INotifyPropertyChanged`.

## Architecture
The framework follows the **Monitor-Service-Entity (MSE)** pattern, ensuring a clear separation of concerns.

![MSE Pattern Diagram](assets/mse_pattern_diagram.png)

## Key Components

### ContextMonitor
Abstract base class for data producers.
- **Update Types**: Continuous (event-based), Interval (polling), or OnRequest.
- **Threading**: Monitors typically run their own background tasks to avoid blocking the main application.
- **Usage**: Inherit from this to create specific sensors (e.g., `LocationMonitor`, `BatteryMonitor`).

### ContextService
Abstract base class for data coordinators.
- **Persistence**: Built-in support for `Periodic`, `OnRequest`, or `Combined` persistence strategies.
- **Integration**: Designed to work seamlessly with the `MulticastTransportFramework` for distributed context sharing.
- **Threading**: Updates are received on the Monitor's background thread. UI marshalling must be handled manually if needed.

### IEntity
Interface for context data objects.
- **Reactive**: Implements `INotifyPropertyChanged` for direct integration with WPF/MAUI/Blazor data binding.
- **Passive**: Should ideally remain a plain data holder (POCO) to simplify serialization.

## Usage Example

```csharp
// 1. Define Entity
public class RoomTemperature : IEntity { ... }

// 2. Define Monitor
public class TempSensorMonitor : ContextMonitor { ... }

// 3. Define Service
public class HVACService : ContextService
{
    private readonly RoomTemperature _entity = new RoomTemperature();

    public HVACService(TempSensorMonitor monitor)
    {
        // Manual subscription
        monitor.OnNotifyContextServices += this.UpdateMonitorReading;
    }
    
    protected override void CustomUpdateMonitorReading(object sender, NotifyContextMonitorListenersEventArgs e)
    {
        // Update logic here (Runs on background thread)
        // Note: If binding _entity to UI, you must marshal this update to the UI thread manually.
        // _entity.Value = ...
    }
}
```

## Dependencies
- `Microsoft.Extensions.Logging.Abstractions`
