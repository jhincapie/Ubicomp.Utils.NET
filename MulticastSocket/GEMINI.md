# MulticastSocket Context

## Project Scope
**MulticastSocket** is the foundational networking library for the Ubicomp.Utils.NET solution. It wraps standard .NET UDP sockets to provide specific multicast capabilities.

## Key Components
*   **`MulticastSocket`**: The main class. Handles socket creation, binding, joining multicast groups, and async I/O.
    *   *Constructor*: Takes Target IP, Port, and TTL.
    *   *Events*: Uses `NotifyMulticastSocketListener` delegate.
*   **`IMulticastSocketListener`**: Interface for classes that want to listen to socket events (though the class uses a standard C# event `OnNotifyMulticastSocketListener`).
*   **`MulticastSocketMessageType`**: Enum defining event types: `SocketStarted`, `MessageReceived`, `ReceiveException`, `MessageSent`, `SendException`.

## Implementation Details
*   **Threading**: Incoming messages are offloaded to the `ThreadPool` before firing events. This ensures the receive loop (`Recieve` -> `ReceiveCallback`) remains responsive.
*   **Buffer Management**: A `StateObject` class manages buffers. Buffers are copied before being passed to listeners to ensure thread safety.
*   **Socket Options**: Sets `ReuseAddress` (SO_REUSEADDR) to allow multiple apps to bind the same port on the same machine (useful for testing).

## Usage
This library is rarely used directly by the end-user application; it is primarily a dependency for the **MulticastTransportFramework**.
