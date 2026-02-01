# ContextAwarenessFramework

A framework for building context-aware applications by decoupling context sensing (`ContextMonitor`) from context management and application logic (`ContextService`).

## Overview
The **ContextAwarenessFramework** provides a standard structure for:
1.  **Sensing**: Acquiring data from sensors or software events via `ContextMonitor`.
2.  **Processing**: Aggregating and transforming raw data via `ContextService`.
3.  **Data Modeling**: Representing context using `IEntity` objects.
4.  **Notification**: Thread-safe updates to UI components (WPF/WinForms friendly via `Dispatcher`).

## Class Diagram
![ContextAwarenessFramework Class Diagram](assets/class_diagram.png)

## Key Components

### ContextMonitor
Abstract base class for data producers.
- **Update Types**: Continuous, Interval, or OnRequest.
- **Events**: Fires `OnNotifyContextServices` when new data is available.
- **Usage**: Inherit from this to create specific sensors (e.g., `LocationMonitor`, `BatteryMonitor`).

### ContextService
Abstract base class for data consumers/managers.
- **Implements**: `IContextMonitorListener`.
- **Threading**: Automatically marshals updates to the creating thread's `Dispatcher` (essential for UI updates).
- **Persistence**: Built-in support for periodic or request-based data saving (`PersistEntities`).
- **Logging**: Exposes a `Logger` property (defaults to `NullLogger`) for injecting `Microsoft.Extensions.Logging` implementations.
- **Usage**: Inherit from this to implement logic (e.g., `LocationService` that aggregates GPS and WiFi data).

### IEntity
Interface for data objects.
- **Implements**: `INotifyPropertyChanged` for data binding support.
- **Usage**: Define your domain models (e.g., `UserLocation`) implementing this interface.

## Usage Example

### Defining a Service with Logging
```csharp
public class MyService : ContextService
{
    protected override void CustomUpdateMonitorReading(object sender, NotifyContextMonitorListenersEventArgs e)
    {
        // Handle new data from a monitor
        var data = e.NewObject;
        Logger.LogInformation("Service received data: {Data}", data);
    }
}

// Injecting the logger
var service = new MyService();
service.Logger = loggerFactory.CreateLogger<MyService>();
service.Start();
```

### Defining a Monitor
```csharp
public class MyMonitor : ContextMonitor
{
    protected override void CustomRun()
    {
        // Poll sensor or wait for event
        var data = "New Value";
        NotifyContextServices(this, new NotifyContextMonitorListenersEventArgs(typeof(string), data));
    }
}
```

## Dependencies
- `System.Windows.Threading` (WPF/Base)
- `Microsoft.Extensions.Logging.Abstractions`