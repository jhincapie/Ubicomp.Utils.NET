# MulticastSocket

**MulticastSocket** is a high-performance, asynchronous wrapper around standard .NET UDP sockets, designed specifically for multicast communication. It targets **.NET 8.0** and leverages modern features like `IAsyncEnumerable`, `System.Threading.Channels`, and `ArrayPool` to minimize allocations and maximize throughput.

## Key Features
*   **Reactive API**: Exposes incoming messages as an `IAsyncEnumerable<SocketMessage>` stream via `GetMessageStream()`.
*   **Zero-Allocation**: Uses `ArrayPool<byte>` and `ObjectPool<SocketMessage>` to reduce Garbage Collection (GC) pressure during high-frequency updates.
*   **Thread Safety**: Decouples the high-speed receive loop from message processing using bounded channels.
*   **Flexible Configuration**: Fluent builder pattern (`MulticastSocketBuilder`) and safe option factories (`MulticastSocketOptions`).

## Installation
The library is part of the `Ubicomp.Utils.NET` solution. Ensure your project targets `.NET 8.0`.

## Usage

### 1. Initialization
Use the `MulticastSocketBuilder` to configure and build the socket.

```csharp
using Ubicomp.Utils.NET.Sockets;
using Microsoft.Extensions.Logging;

// Create options using factory methods
var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5000);
// Or for WAN: MulticastSocketOptions.WideAreaNetwork("239.0.0.1", 5000, ttl: 16);

// Build the socket
using var socket = new MulticastSocketBuilder()
    .WithOptions(options)
    .WithLogging(loggerFactory) // Optional
    .Build();

// Join the multicast group
socket.StartReceiving();
```

### 2. Receiving Messages (Async Stream)
The recommended way to consume messages is via the async stream.

```csharp
var cts = new CancellationTokenSource();

try
{
    await foreach (var msg in socket.GetMessageStream(cts.Token))
    {
        // Access data (Memory<byte>)
        Console.WriteLine($"Received {msg.Length} bytes from {msg.RemoteEndpoint}");

        // Process data...
        // Note: The message is automatically returned to the pool after the loop iteration.
    }
}
catch (OperationCanceledException)
{
    // Graceful shutdown
}
```

### 3. Sending Messages
You can send `string`, `byte[]`, or `ReadOnlyMemory<byte>` (preferred).

```csharp
// Zero-allocation send
byte[] buffer = Encoding.UTF8.GetBytes("Hello Multicast!");
await socket.SendAsync(new ReadOnlyMemory<byte>(buffer));

// String helper
await socket.SendAsync("Hello World");
```

## Architecture
*   **`MulticastSocket`**: Manages the underlying `System.Net.Sockets.Socket`. It runs a background `ReceiveAsyncLoop` that pushes data into a `Channel<SocketMessage>`.
*   **`MulticastSocketOptions`**: Configuration container.
    *   `LocalNetwork()`: Sets TTL=1 and applies an interface filter for private IP ranges (10.x, 172.16-31.x, 192.168.x).
    *   `WideAreaNetwork()`: Sets higher TTL (default 16) without interface filtering.
*   **`SocketMessage`**: A pooled object wrapping the received data. It implements `IDisposable` to return itself to the pool.

## Testing
The library includes `InMemoryMulticastSocket` for unit testing without network I/O.

```csharp
var mockSocket = new InMemoryMulticastSocket();
// Inject mockSocket into your components
```
