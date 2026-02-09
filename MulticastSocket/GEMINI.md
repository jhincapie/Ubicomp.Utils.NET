# MulticastSocket Context

## Project Scope
**MulticastSocket** is the foundational networking library for the Ubicomp.Utils.NET solution. It wraps standard .NET UDP sockets to provide specific multicast capabilities with a modern, fluent, and reactive API.

## Architecture

![Class Diagram](assets/class_diagram.png)

## Key Components
*   **`MulticastSocketBuilder`**: The primary entry point for configuration. Use this to set options, filters, and callbacks.
*   **`MulticastSocket`**: The core engine. Handles socket creation, binding, joining multicast groups, and async I/O.
    *   **Streaming**: `GetMessageStream()` returns an `IAsyncEnumerable<SocketMessage>`, allowing for modern, reactive consumption using `await foreach`.
    *   **Decoupling**: Uses `System.Threading.Channels` to decouple the high-speed receive loop from the processing logic.
*   **`SocketMessage`**: Represents a received packet.
    *   **Pooling**: Utilizes `ObjectPool<SocketMessage>` and `ArrayPool<byte>` to minimize Garbage Collection (GC) pressure in high-throughput scenarios.
*   **`InMemoryMulticastSocket`**: A testing utility that simulates multicast traffic in-memory using a shared dictionary, avoiding OS networking stack calls.

## Implementation Details
*   **Target Framework**: **.NET 8.0**
*   **Async I/O**: Uses `ReceiveFromAsync` with `Memory<byte>` for modern, high-performance async I/O.
*   **Threading**: A dedicated `ReceiveAsyncLoop` offloads incoming data to a bounded Channel. Consumers process messages from this channel.
*   **Socket Options**: Configurable via `MulticastSocketOptions` factory methods (e.g., `LocalNetwork`).
*   **Sequence ID**: Assigns a monotonic sequence ID to every received packet, enabling ordering logic in higher layers.

## Do's and Don'ts
*   **Do** use `GetMessageStream()` for consuming messages in a modern, async-friendly way.
*   **Do** use `MulticastSocketOptions.LocalNetwork(...)` or `WideAreaNetwork(...)` factory methods.
*   **Don't** use the legacy `ReceiveCallback` or `StateObject`.
*   **Don't** forget to `Dispose()` the socket or the messages if manual handling is required.

## Usage
Used by **MulticastTransportFramework**, but valid for any low-level multicast needs.

```csharp
var socket = new MulticastSocketBuilder()
    .WithOptions(MulticastSocketOptions.LocalNetwork("239.0.0.1", 5000))
    .Build();

await socket.JoinGroupAsync();

await foreach (var msg in socket.GetMessageStream())
{
    // Process msg
}
```
