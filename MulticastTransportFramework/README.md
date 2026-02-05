# MulticastTransportFramework

**MulticastTransportFramework** implements a higher-level messaging protocol over UDP multicast.

## Architecture

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

![MulticastTransportFramework Class Diagram](assets/class_diagram.png)

![MulticastTransportFramework Flow Diagram](assets/transport_flow_diagram.png)

## Core Logic
1.  **Incoming Data**: `MulticastSocket` receives bytes into a **pooled buffer** (`ArrayPool<byte>`) to minimize GC pressure.
2.  **Consumption**: `TransportComponent` consumes messages from the socket's `IAsyncEnumerable<SocketMessage>` stream.
3.  **Deserialization**: `BinaryPacket.Deserialize` parses the byte stream directly.
    *   **Polymorphism**: The `TransportComponent` resolves the concrete type of `MessageData` using the registered string ID.
4.  **GateKeeper**: Optional mechanism that ensures sequential processing of messages.
5.  **Dispatch**:
    *   Strongly-typed handlers receive the data POCO and a `MessageContext`.
    *   **Auto-Ack**: If enabled, an acknowledgement is sent automatically if requested.

## Usage

### 1. Define your Messages
Decorate your message POCOs with the `[MessageType]` attribute.

```csharp
[MessageType("sensor.data")]
public class SensorData
{
    public double Value { get; set; }
}
```

### 2. Configure and Build

```csharp
var options = MulticastSocketOptions.WideAreaNetwork("239.0.0.1", 5000);

var transport = new TransportBuilder()
    .WithMulticastOptions(options)
    .WithLogging(loggerFactory)
    .WithSecurityKey("SuperSecretKey") // Enables AES-GCM
    .WithEncryption(true)
    .Build();

// Auto-discover message types (Source Generator)
transport.RegisterDiscoveredMessages();

transport.RegisterHandler<SensorData>((data, context) =>
{
    Console.WriteLine($"Received: {data.Value}");
});

transport.Start();
```

### 3. Sending Messages

```csharp
// Simple send (Encrypted & Binary Serialized automatically)
await transport.SendAsync(new SensorData { Value = 25.5 });

// Send with acknowledgment request
await transport.SendAsync(new SensorData { Value = 25.5 }, new SendOptions { RequestAck = true });
```

## Internal Components
*   **`TransportBuilder`**: Primary entry point.
*   **`TransportComponent`**: Orchestrator.
*   **`MessageContext`**: Metadata (Source, Timestamp, Ack).
*   **`BinaryPacket`**: Handles the binary wire format.

## Dependencies
- `MulticastSocket`
- `System.Text.Json`
- `Microsoft.Extensions.Logging.Abstractions`
