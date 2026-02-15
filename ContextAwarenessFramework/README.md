# ContextAwarenessFramework

**ContextAwarenessFramework** is a library for building context-aware applications by decoupling context sensing (`ContextMonitor`) from context management and application logic (`ContextService`) using the **Monitor-Service-Entity (MSE)** pattern.

## Overview
The framework provides a standard structure for:
1.  **Sensing**: Acquiring data from sensors or external events via `ContextMonitor`.
2.  **Processing**: Aggregating, transforming, and persisting context via `ContextService`.
3.  **Data Modeling**: Representing state using thread-safe `IEntity` objects.
4.  **Notification**: Seamless UI binding support via `INotifyPropertyChanged`.

## Architecture
The framework follows the **Monitor-Service-Entity (MSE)** pattern, ensuring a clear separation of concerns.

### 1. ContextMonitor
Abstract base class for data producers.
- **Update Types**: Continuous (event-based), Interval (polling), or OnRequest.
- **Threading**: Monitors typically run their own background tasks to avoid blocking the main application.
- **Usage**: Inherit from this to create specific sensors (e.g., `LocationMonitor`, `BatteryMonitor`).

### 2. ContextService
Abstract base class for data coordinators.
- **Persistence**: Built-in support for `Periodic`, `OnRequest`, or `Combined` persistence strategies.
- **Integration**: Designed to work seamlessly with the `MulticastTransportFramework` for distributed context sharing.
- **Threading**: Updates are received on the Monitor's background thread. **UI marshalling must be handled manually** (the base class does not capture `Dispatcher`).

### 3. IEntity
Interface for context data objects.
- **Reactive**: Implements `INotifyPropertyChanged` for direct integration with WPF/MAUI/Blazor data binding.
- **Passive**: Should ideally remain a plain data holder (POCO) to simplify serialization.

## Usage

### 1. Define Components

The framework uses the Template Method pattern. You should override the protected `Custom*` methods instead of the public lifecycle methods.

*   `CustomStart()`: Called when the component is started. Initialize resources here.
*   `CustomStop()`: Called when the component is stopped. Cleanup resources here.
*   `CustomRun()`: (Monitors Only) Called repeatedly by the container's background thread if `UpdateType` is set to `Continuous` or `Interval`.

```csharp
using Ubicomp.Utils.NET.ContextAwarenessFramework.DataModel;
using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextAdapter;
using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextService;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// 1. Define Entity
public class RoomTemperature : IEntity, INotifyPropertyChanged
{
    private double _value;
    public double Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// 2. Define Monitor
public class TempSensorMonitor : ContextMonitor
{
    public TempSensorMonitor()
    {
        // Configure update strategy
        UpdateType = ContextAdapterUpdateType.Interval;
        UpdateInterval = 1000;
    }

    protected override void CustomStart()
    {
        Console.WriteLine("Sensor Started");
    }

    protected override void CustomRun()
    {
        // This runs on a background thread every 1000ms
        var reading = new RoomTemperature { Value = 22.5 };
        NotifyContextServices(this, new NotifyContextMonitorListenersEventArgs(typeof(RoomTemperature), reading));
    }
}

// 3. Define Service
public class HVACService : ContextService
{
    public HVACService() { }

    protected override void CustomUpdateMonitorReading(object sender, NotifyContextMonitorListenersEventArgs e)
    {
        // IMPORTANT: This runs on the Monitor's background thread!
        if (e.NewObject is RoomTemperature temp)
        {
            Console.WriteLine($"Current Temp: {temp.Value}");
        }
    }
}
```

### 2. Wiring it Up (The Container Way)
The framework provides static containers to manage the lifecycle of your components.

```csharp
using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextAdapter;
using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextService;

// Setup
var monitor = new TempSensorMonitor();
var service = new HVACService();

// Subscribe
monitor.OnNotifyContextServices += service.UpdateMonitorReading;

// Register & Start
ContextMonitorContainer.AddMonitor(monitor);
ContextServiceContainer.AddContextService(service);

ContextMonitorContainer.StartMonitors();
ContextServiceContainer.StartServices();

// ... Application runs ...

// Shutdown
ContextMonitorContainer.StopMonitors();
ContextServiceContainer.StopServices();
```
