# MulticastSocket

A lightweight .NET wrapper around `System.Net.Sockets.Socket` to simplify multicast UDP communication with a modern, fluent API.

## Overview
The **MulticastSocket** library provides a clean, asynchronous interface for joining multicast groups, sending byte arrays, and receiving data. It abstracts raw socket complexity while ensuring strict message ordering via sequence IDs.

## Class Diagram
![MulticastSocket Class Diagram](assets/class_diagram.png)

## Message Flow
![MulticastSocket Message Flow](assets/message_flow_diagram.png)

## Features
- **Fluent Builder API**:guided setup for network options and callbacks.
- **Strongly-Typed Callbacks**: Clean `Action` based events for messages, errors, and status.
- **Ordered Metadata**: Every message carries a `SequenceId` and `Timestamp`.
- **Cross-Platform**: Handles platform-specific socket options automatically.
- **Asynchronous I/O**: High-performance, non-blocking receive and send loops.

## Usage

### Initialization & Receiving
Use the `MulticastSocketBuilder` to configure your connection and register handlers.

```csharp
using Ubicomp.Utils.NET.Sockets;

var socket = new MulticastSocketBuilder()
    .WithLocalNetwork(port: 5000)
    .OnMessageReceived(msg => 
    {
        Console.WriteLine($"Received {msg.Data.Length} bytes. Seq: {msg.SequenceId}");
    })
    .OnError(err => Console.Error.WriteLine($"Socket Error: {err.Message}"))
    .OnStarted(() => Console.WriteLine("Socket listening..."))
    .Build();

socket.StartReceiving();
```

### Sending Messages
```csharp
// Send raw bytes
byte[] data = Encoding.UTF8.GetBytes("Hello Multicast!");
await socket.SendAsync(data);

// Or use the string overload
await socket.SendAsync("Hello Multicast!");
```

### Advanced Configuration
Fine-tune behavior via `MulticastSocketOptions`:

```csharp
var options = MulticastSocketOptions.WideAreaNetwork("239.0.0.2", 5000, ttl: 2);
options.ReceiveBufferSize = 65536;
options.InterfaceFilter = addr => IPAddress.IsLoopback(addr);

using var socket = new MulticastSocketBuilder()
    .WithOptions(options)
    .OnMessageReceived(msg => { /* ... */ })
    .Build();
```

## Implementation Details
- **Threading**: Callbacks are dispatched on the `ThreadPool` to keep the receive loop responsive.
- **Sequence Management**: A monotonically increasing ID is assigned to each packet upon arrival.
- **Interface Management**: Automatically joins all valid multicast-capable IPv4 interfaces unless a filter is provided.

## Dependencies
- `System.Net`
- `System.Net.Sockets`
