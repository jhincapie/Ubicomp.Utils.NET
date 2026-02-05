# MulticastSocket

**MulticastSocket** is the foundational networking library for the Ubicomp.Utils.NET solution. It wraps standard .NET UDP sockets to provide specific multicast capabilities with a modern, fluent, and reactive API.

## Key Components
*   **`MulticastSocketBuilder`**: The primary entry point for configuration. Use this to set options, filters, and callbacks.
*   **`MulticastSocket`**: The core engine. Handles socket creation, binding, joining multicast groups, and async I/O.
    *   **Streaming**: `GetMessageStream()` returns an `IAsyncEnumerable<SocketMessage>`, allowing for modern, reactive consumption using `await foreach`.
    *   **Decoupling**: Uses `System.Threading.Channels` to decouple the high-speed receive loop from the processing logic.
*   **`SocketMessage`**: Represents a received packet.
    *   **Pooling**: Utilizes `ObjectPool<SocketMessage>` and `ArrayPool<byte>` to minimize Garbage Collection (GC) pressure.
*   **`SocketErrorContext`**: Provides details about runtime exceptions.

## Diagrams
![MulticastSocket Class Diagram](assets/class_diagram.png)

## Implementation Details
*   **Threading**: A dedicated `ReceiveAsyncLoop` offloads incoming data to a bounded `Channel`. Consumers process messages from this channel, ensuring the socket remains responsive.
*   **Async I/O**: Fully Task-based API (`SendAsync`, `ReceiveAsync`) for high performance and scalability.
*   **Buffer Management**: Uses `ArrayPool<byte>` for zero-allocation buffer management in the receive loop.
*   **Sequence ID**: Assigns a monotonic sequence ID to every received packet, which is critical for higher-level ordering logic (e.g., in the Transport layer).
*   **Socket Options**: Sets `ReuseAddress` (SO_REUSEADDR) to allow multiple applications to bind to the same port on the same host.

## Usage

### Initialization & Receiving (Async Stream)
The recommended way to receive messages is via the `GetMessageStream` method.

```csharp
using Ubicomp.Utils.NET.Sockets;

var socket = new MulticastSocketBuilder()
    .WithLocalNetwork(port: 5000)
    .WithLogging(loggerFactory)
    .Build();

await socket.JoinGroupAsync();

// Consume messages as an async stream
await foreach (var msg in socket.GetMessageStream(cts.Token))
{
    Console.WriteLine($"Received {msg.Data.Length} bytes. Seq: {msg.ArrivalSequenceId}");
    // Note: msg.Dispose() is called automatically by the stream enumerator
}
```

### Sending Messages
```csharp
// Send raw bytes
byte[] data = Encoding.UTF8.GetBytes("Hello Multicast!");
await socket.SendAsync(data);

// Or use the string overload
await socket.SendAsync("Hello Multicast!");
```

## Dependencies
- `Microsoft.Extensions.Logging.Abstractions`
- `System.Threading.Channels`