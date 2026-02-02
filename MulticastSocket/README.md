# MulticastSocket

A lightweight .NET wrapper around `System.Net.Sockets.Socket` to simplify multicast UDP communication.

## Overview
The **MulticastSocket** library provides an easy-to-use interface for joining multicast groups, sending messages, and receiving data asynchronously. It abstracts the complexities of raw socket configuration, thread management for callbacks, and buffer handling.

## Class Diagram
![MulticastSocket Class Diagram](assets/class_diagram.png)

## Features
- **Simplified Setup**: Configures TTL, Loopback, and Bind options automatically.
- **Cross-Platform**: Handles platform-specific socket options gracefully (e.g., skips `NoDelay` on Linux/UDP where unsupported).
- **Asynchronous I/O**: Uses `BeginReceiveFrom` and `BeginSendTo` for non-blocking operations.
- **Threaded Events**: Dispatches events (`OnNotifyMulticastSocketListener`) on the `ThreadPool` to avoid blocking the network thread.
- **Error Handling**: Captures and reports socket exceptions via events.

## Usage

### Initialization
```csharp
using Ubicomp.Utils.NET.Sockets;

// Initialize: Target IP, Port, TTL
MulticastSocketOptions options = new MulticastSocketOptions("224.0.0.1", 5000)
{
    TimeToLive = 1
};
MulticastSocket mSocket = new MulticastSocket(options);
```

### Receiving Messages
```csharp
mSocket.OnNotifyMulticastSocketListener += (sender, e) => {
    switch (e.Type)
    {
        case MulticastSocketMessageType.MessageReceived:
            byte[] data = (byte[])e.NewObject;
            Console.WriteLine($"Received {data.Length} bytes.");
            break;
        case MulticastSocketMessageType.SocketStarted:
            Console.WriteLine("Listening...");
            break;
    }
};

mSocket.StartReceiving();
```

### Sending Messages
```csharp
mSocket.Send("Hello Multicast!");
```

### Advanced Configuration
You can fine-tune the socket behavior using `MulticastSocketOptions`:

```csharp
var options = new MulticastSocketOptions("239.0.0.1", 5000)
{
    TimeToLive = 2,
    ReuseAddress = true,
    NoDelay = true,
    ReceiveBufferSize = 65536,
    // Join only the loopback interface for testing
    InterfaceFilter = addr => IPAddress.IsLoopback(addr)
};

using var socket = new MulticastSocket(options);
```

The options object also provides a `Validate()` method (called automatically by the constructor) to ensure your settings (like IP range and ports) are correct.

## Implementation Details
- **Multicast Group Management**: Automatically joins all valid IPv4 interfaces by default.
- **Buffer Management**: Uses a `StateObject` with a internal buffer to manage asynchronous receives.
- **Socket Options**: Sets `ReuseAddress` to allow multiple applications to share the same port.

## Dependencies
- `System.Net`
- `System.Net.Sockets`
