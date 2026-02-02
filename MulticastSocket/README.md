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

// Initialize: GroupAddress (Optional, defaults to 239.0.0.1), Port (Optional, defaults to 5000)
MulticastSocketOptions options = MulticastSocketOptions.LocalNetwork(port: 5000);
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
// Factory methods validate basic settings (GroupAddress, Port) internally
var options = MulticastSocketOptions.LocalNetwork("239.0.0.2", 5000);
options.TimeToLive = 2;
options.ReuseAddress = true;
options.NoDelay = true;
options.ReceiveBufferSize = 65536;

// Join only the loopback interface for testing
options.InterfaceFilter = addr => IPAddress.IsLoopback(addr);

using var socket = new MulticastSocket(options);
```

The options object also provides a `Validate()` method (called automatically by factory methods and the `MulticastSocket` constructor) to ensure your settings are correct.

## Implementation Details
- **Multicast Group Management**: Automatically joins all valid IPv4 interfaces by default.
- **Buffer Management**: Uses a `StateObject` with a internal buffer to manage asynchronous receives.
- **Socket Options**: Sets `ReuseAddress` to allow multiple applications to share the same port.

## Dependencies
- `System.Net`
- `System.Net.Sockets`