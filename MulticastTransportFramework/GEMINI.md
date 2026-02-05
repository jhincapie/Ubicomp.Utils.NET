# MulticastTransportFramework Context

## Architecture
**MulticastTransportFramework** implements a higher-level messaging protocol over UDP multicast.

*   **Fluent Builder**: Use `TransportBuilder` to configure and create a `TransportComponent`.
*   **Strongly-Typed Messaging**: Generic `SendAsync<T>` methods handle internal serialization and routing via `[MessageType("id")]` attributes.
*   **Binary Protocol**: Uses a custom binary format (`BinaryPacket`) to eliminate double serialization (JSON payload inside JSON envelope).
    *   **Reduced Overhead**: Payload size reduced by ~66% compared to legacy JSON envelope.
    *   **Compatibility**: Falls back to legacy JSON format if `Magic Byte` is not present, ensuring backward compatibility.
*   **Security (AES-GCM)**:
    *   **Modern Runtimes**: Uses `System.Security.Cryptography.AesGcm` (standard 2.1+) for authenticated encryption.
    *   **Legacy Runtimes**: Falls back to `Aes` (CBC Mode) + HMAC for standard 2.0.
    *   **Key Derivation**: Uses HKDF-like scheme to derive Integrity and Encryption keys from a single `SecurityKey`.
*   **POCO Support**: Any class can be used as message content; no marker interface is required.
*   **Diagnostic Transparency**: Uses `Microsoft.Extensions.Logging.ILogger` across both the transport and socket layers.
*   **Auto-Discovery**: Source Generator automatically registers types with `[MessageType]` attribute (via `transport.RegisterDiscoveredMessages()`).

## Core Logic
1.  **Incoming Data**: `MulticastSocket` receives bytes into a **pooled buffer** (`ArrayPool<byte>`) to minimize GC pressure.
2.  **Consumption**: `TransportComponent` consumes messages from the socket's `IAsyncEnumerable<SocketMessage>` stream.
3.  **Deserialization**: `BinaryPacket.Deserialize` parses the byte stream directly.
    *   **Polymorphism**: The `TransportComponent` resolves the concrete type of `MessageData` using the registered string ID.
4.  **GateKeeper**: Optional mechanism that ensures sequential processing of messages.
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
