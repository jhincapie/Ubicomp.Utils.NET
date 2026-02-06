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
*   **`InMemoryMulticastSocket`**: An in-memory implementation of `IMulticastSocket` for unit testing.
    *   **Simulation**: Uses a shared `ConcurrentDictionary` and `Channel` to simulate a multicast network bus within the same process.

## Diagrams
![MulticastSocket Class Diagram](assets/class_diagram.png)

## Implementation Details
*   **Multi-Targeting**:
    *   **.NET 8.0+**: Uses high-performance `ReceiveFromAsync` (Memory-based) and `CancellationToken`.
    *   **.NET Standard 2.0**: Uses a compatibility wrapper around legacy APM `BeginReceiveFrom` / `EndReceiveFrom` to simulate async behavior (`ReceiveAsyncLoop`).
*   **Threading**: A dedicated loop offloads incoming data to a bounded `Channel`. Consumers process messages from this channel, ensuring the socket remains responsive.
*   **Buffer Management**: Uses `ArrayPool<byte>` for zero-allocation buffer management in the receive loop.
*   **Socket Options**:
    *   **Strict Adherence**: Boolean options (`NoDelay`, `ReuseAddress`) are strictly enforced (set to 1 or 0) regardless of OS defaults.
    *   **Error Handling**: `SocketOptionName.NoDelay` is wrapped in a `try-catch` block as some platforms/drivers throw on this option for UDP.

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
