# MulticastSocket Context

## Project Scope
**MulticastSocket** is the foundational networking library for the Ubicomp.Utils.NET solution. It wraps standard .NET UDP sockets to provide specific multicast capabilities with a modern, fluent API.

## Key Components
*   **`MulticastSocketBuilder`**: The primary entry point for configuration. Use this to set options, filters, and callbacks, and now supports `WithLogging` for diagnostic transparency.
*   **`MulticastSocket`**: The core engine. Handles socket creation, binding, joining multicast groups, and async I/O.
    *   *Instantiation*: Via `MulticastSocketBuilder`.
    *   *Callbacks*: Uses strongly-typed `Action` delegates (`OnMessageReceived`, `OnError`, `OnStarted`).
    *   *Reactive Streams*: Provides `GetMessageStream()` which returns an `IAsyncEnumerable<SocketMessage>` for modern streaming processing.
*   **`SocketMessage`**: Represents a received packet with data, sequence ID, and timestamp.
*   **`SocketErrorContext`**: Provides details about runtime exceptions.

## Implementation Details
*   **Threading**: Incoming messages are offloaded to the `ThreadPool` before firing callbacks and pushing to the message stream. This ensures the receive loop remains responsive.
*   **Diagnostics**: Built-in support for `ILogger` provides granular insights into socket lifecycle, interface joining, and error conditions.
*   **Async I/O**: Supports modern Task-based sending via `SendAsync`, wrapping the internal Begin/End pattern for high performance and better developer experience.
*   **Buffer Management**: A `StateObject` class manages internal buffers. Data is copied into `SocketMessage` objects before being passed to consumers.
*   **Sequence ID**: Every received message is assigned a consecutive ID used by higher layers (e.g., `TransportComponent`) to maintain order.
*   **Socket Options**: Sets `ReuseAddress` (SO_REUSEADDR) to allow multiple apps to bind the same port on the same machine.

## Usage
This library is primarily a dependency for the **MulticastTransportFramework**, but can be used directly for low-level byte-based multicast communication.