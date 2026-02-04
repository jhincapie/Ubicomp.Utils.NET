# MulticastTransportFramework Context

## Architecture
**MulticastTransportFramework** implements a higher-level messaging protocol over UDP multicast.

*   **Fluent Builder**: Use `TransportBuilder` to configure and create a `TransportComponent`.
*   **Strongly-Typed Messaging**: Generic `SendAsync<T>` methods handle internal serialization and routing via `[MessageType("id")]` attributes.
*   **POCO Support**: Any class can be used as message content; no marker interface is required.
*   **Serialization**: Uses `System.Text.Json` for serialization and efficient polymorphic deserialization.
*   **Diagnostic Transparency**: Uses `Microsoft.Extensions.Logging.ILogger` across both the transport and socket layers.
*   **Message Routing**: Dispatches messages to strongly-typed handlers based on their string ID defined in the `MessageTypeAttribute`.

## Core Logic
1.  **Incoming Data**: `MulticastSocket` receives bytes and pushes them into an internal `Channel`.
2.  **Consumption**: `TransportComponent` consumes messages from the socket's `IAsyncEnumerable<SocketMessage>` stream.
3.  **Deserialization**: `System.Text.Json` deserializes the UTF-8 string into a `TransportMessage` envelope.
    *   **Polymorphism**: The `TransportComponent` resolves the concrete type of `MessageData` (from `JsonElement`) using the registered string ID.
4.  **GateKeeper**: Optional mechanism (controlled by `EnforceOrdering` in options) that ensures sequential processing of messages, preserving the order assigned by the socket layer.
5.  **Dispatch**:
    *   Strongly-typed handlers receive the data POCO and a `MessageContext`.
    *   **Auto-Ack**: If enabled, an acknowledgement is sent automatically if requested.

## Key Classes
*   **`TransportBuilder`**: The primary entry point for configuration.
*   **`TransportComponent`**: The orchestrator managing the socket and message flow.
*   **`MessageContext`**: Provides metadata (Source, Timestamp, RequestAck) to message handlers.
*   **`TransportMessage`**: The internal data envelope (hidden from common usage).

## Dependencies
*   **Internal**: `MulticastSocket`
*   **External**: `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`