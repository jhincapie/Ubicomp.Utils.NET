# MulticastTransportFramework Context

## Architecture
**MulticastTransportFramework** implements a higher-level messaging protocol over UDP multicast.

*   **Fluent Builder**: Use `TransportBuilder` to configure and create a `TransportComponent`.
*   **Strongly-Typed Messaging**: Generic `Send<T>` (synchronous) and `SendAsync<T>` (modern Task-based) methods handle internal serialization and routing.
*   **POCO Support**: Any class can be used as message content; no marker interface is required.
*   **Serialization**: Uses `Newtonsoft.Json` to convert between .NET objects and JSON strings.
*   **Logging**: Uses `Microsoft.Extensions.Logging.ILogger`, typically injected via the builder.
*   **Message Routing**: Dispatches messages to strongly-typed handlers or legacy `ITransportListener`s based on their `MessageType` integer ID.

## Core Logic
1.  **Incoming Data**: `MulticastSocket` receives bytes.
2.  **Conversion**: Bytes are converted to a UTF-8 string.
3.  **Deserialization**: `Newtonsoft.Json` deserializes the string into a `TransportMessage` "envelope" object.
    *   **Polymorphism**: The internal `TransportMessageConverter` handles the concrete type of the `MessageData` property based on the registered ID.
4.  **GateKeeper**: Optional mechanism (controlled by `EnforceOrdering` in options) that ensures sequential processing of messages, preserving the order assigned by the socket layer. It employs a buffering strategy for out-of-order messages and a recovery timeout (`GateKeeperTimeout`) to automatically advance the sequence if a message is lost, preventing deadlocks.
5.  **Dispatch**:
    *   Strongly-typed handlers receive the data POCO and a `MessageContext`.
    *   Legacy `ITransportListener`s receive the full `TransportMessage`.
    *   **Auto-Ack**: If enabled, an acknowledgement is sent automatically if requested.

## Key Classes
*   **`TransportBuilder`**: The primary entry point for configuration.
*   **`TransportComponent`**: The orchestrator managing the socket and message flow.
*   **`MessageContext`**: Provides metadata (Source, Timestamp, RequestAck) to message handlers.
*   **`TransportMessage`**: The internal data envelope (hidden from common usage).

## Dependencies
*   **Internal**: `MulticastSocket`
*   **External**: `Newtonsoft.Json`, `Microsoft.Extensions.Logging.Abstractions`