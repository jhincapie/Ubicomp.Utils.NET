# MulticastSocket Context

## Module Purpose
**MulticastSocket** is the foundational networking layer for the `Ubicomp.Utils.NET` solution. It abstracts the complexity of `System.Net.Sockets` for UDP multicast operations, providing a modern, async-first API.

## Architectural Context
*   **Layer**: Bottom-most layer (Networking Hardware Abstraction).
*   **Used By**: `MulticastTransportFramework` (via `TransportComponent`).
*   **Dependencies**: `System.Net.Sockets`, `System.Threading.Channels`, `Microsoft.Extensions.Logging`.

## Key Components

### 1. `IMulticastSocket`
*   **Interface**: Defines the contract for multicast operations (`SendAsync`, `GetMessageStream`, `StartReceiving`).
*   **Mocking**: `InMemoryMulticastSocket` implements this for testing.

### 2. `MulticastSocket`
*   **Implementation**: Wraps `System.Net.Sockets.Socket`.
*   **Threading**:
    *   **Receive Loop**: Runs on a background `Task`. Reads from socket -> writes to `Channel`.
    *   **Consumption**: Consumers iterate `GetMessageStream()` which reads from the `Channel`.
*   **Memory Management**:
    *   Uses `ArrayPool<byte>.Shared` to rent buffers for `ReceiveFromAsync`.
    *   Uses `ObjectPool<SocketMessage>` to reuse message envelopes.
    *   **Zero-Copy**: Passes `Memory<byte>` slices to consumers.

### 3. `MulticastSocketOptions`
*   **Configuration**: Stores IP, Port, TTL, Buffer Sizes.
*   **Factories**:
    *   `LocalNetwork(ip, port)`: TTL 1, auto-filters private IPs.
    *   `WideAreaNetwork(ip, port, ttl)`: Configurable TTL.

## Do's and Don'ts

### Do's
*   **Do** use `GetMessageStream()` for consuming messages. It handles backpressure via the bounded channel.
*   **Do** use `MulticastSocketOptions` factory methods instead of the constructor.
*   **Do** prefer `SendAsync(ReadOnlyMemory<byte>)` for high-performance sending.
*   **Do** dispose the socket to release the port and stop the background loop.

### Don'ts
*   **Don't** use `ReceiveCallback` or legacy APM patterns.
*   **Don't** manually bind the socket; let `StartReceiving()` or `JoinGroupAsync()` handle it.
*   **Don't** assume the `SocketMessage.Data` buffer is exactly the size of the payload; always use `SocketMessage.Length`.

## File Structure
*   `MulticastSocket.cs`: Main implementation.
*   `IMulticastSocket.cs`: Interface.
*   `MulticastSocketBuilder.cs`: Fluent builder for socket creation.
*   `MulticastSocketOptions.cs`: Configuration.
*   `SocketMessage.cs`: Pooled message envelope.
*   `InMemoryMulticastSocket.cs`: Test implementation.
