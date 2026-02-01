# MulticastTransportFramework

A high-level messaging framework built on top of `MulticastSocket`, providing JSON-based object serialization and type-based message routing.

## Overview
The **MulticastTransportFramework** abstracts raw socket communication into a structured messaging system. It allows applications to send and receive typed objects (`TransportMessage`) without dealing with byte arrays or raw strings.

**Key Features:**
*   **Modern Serialization:** Uses `Newtonsoft.Json` for robust JSON handling.
*   **Modern Logging:** Uses `Microsoft.Extensions.Logging` abstractions, allowing you to plug in any logger (Console, Debug, Serilog, etc.).
*   **Type Safe:** Polymorphic message content support via `KnownTypes` registration.

## System Diagram
![MulticastTransportFramework System Diagram](assets/system_diagram.png)

## Key Concepts
- **TransportComponent**: A Singleton class that manages the `MulticastSocket`, serialization settings, and listener routing.
- **TransportMessage**: The standard envelope for all communication, containing `MessageId`, `Source`, `Type`, and `Data`.
- **ITransportListener**: Interface for classes that handle specific message types.
- **TransportMessageConverter**: Handles polymorphic deserialization of `MessageData` using a `KnownTypes` dictionary.

## Usage

### Initialization & Logging
You can optionally inject a logger before initialization. If not provided, it defaults to a no-op logger.

```csharp
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Microsoft.Extensions.Logging;

// 1. (Optional) Configure Logging
using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger<TransportComponent> logger = factory.CreateLogger<TransportComponent>();
TransportComponent.Instance.Logger = logger;

// 2. Configure Network Settings
TransportComponent.Instance.MulticastGroupAddress = IPAddress.Parse("224.0.0.1");
TransportComponent.Instance.Port = 6000;
TransportComponent.Instance.UDPTTL = 1;

// 3. Start
TransportComponent.Instance.Init();
```

### Registration (Crucial for Deserialization)
To ensure messages are deserialized into the correct .NET types, you must register the type mapping:

```csharp
// Register that MessageType 101 maps to MyCustomData class
TransportMessageConverter.KnownTypes.Add(101, typeof(MyCustomData));
```

### Sending a Message
```csharp
TransportMessage msg = new TransportMessage()
{
    MessageType = 101, // Custom Type ID
    MessageData = new MyCustomData() { Value = "Hello" }
};

TransportComponent.Instance.Send(msg);
```

### Receiving Messages
Implement `ITransportListener` and register it:

```csharp
public class MyListener : ITransportListener
{
    public MyListener()
    {
        // Register to handle messages of type 101
        TransportComponent.Instance.TransportListeners.Add(101, this);
    }

    public void MessageReceived(TransportMessage message, string rawMessage)
    {
        // Data is already deserialized to the correct type
        if (message.MessageData is MyCustomData data)
        {
            Console.WriteLine($"Received: {data.Value}");
        }
    }
}
```

## Dependencies
- `MulticastSocket` project
- `Newtonsoft.Json`
- `Microsoft.Extensions.Logging.Abstractions`
