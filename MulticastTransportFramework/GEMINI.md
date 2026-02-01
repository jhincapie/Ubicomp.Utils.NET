# MulticastTransportFramework Context

## Architecture
**MulticastTransportFramework** implements a higher-level messaging protocol over UDP multicast.

*   **Singleton Pattern**: `TransportComponent.Instance` is the central access point.
*   **Serialization**: Uses `Newtonsoft.Json` to convert between .NET objects and JSON strings.
*   **Logging**: Uses `Microsoft.Extensions.Logging.ILogger`. The `TransportComponent` exposes a public `Logger` property (defaults to `NullLogger`) for dependency injection.
*   **Message Routing**: Uses a `Dictionary<int, ITransportListener>` to dispatch messages based on their `MessageType` integer ID.

## Core Logic
1.  **Incoming Data**: `MulticastSocket` receives bytes.
2.  **Conversion**: Bytes are converted to a UTF-8 string.
3.  **Deserialization**: `Newtonsoft.Json` deserializes the string into a `TransportMessage` object.
    *   **Polymorphism**: The `TransportMessageConverter` looks up the `MessageType` in the `KnownTypes` dictionary to determine the concrete type of the `MessageData` property.
4.  **GateKeeper**: A synchronization mechanism (`GateKeeperMethod` + `EventWaitHandle`) ensures strict sequential processing of messages, preserving the order of `consecutive` IDs assigned by the socket layer.
5.  **Dispatch**: The `MessageType` is looked up in `TransportListeners`, and `MessageReceived` is called on the registered listener.

## Key Classes
*   **`TransportMessage`**: The data envelope. Has `Guid`, `Source`, `Type`, and `Data` (which implements `ITransportMessageContent`).
*   **`TransportComponent`**: Orchestrator. Manages the socket, logging, and listeners.
*   **`TransportMessageConverter`**: A `JsonConverter` that handles the logic for reading/writing the `MessageData` based on the message type.

## Dependencies
*   **Internal**: `MulticastSocket`
*   **External**: `Newtonsoft.Json`, `Microsoft.Extensions.Logging.Abstractions`
