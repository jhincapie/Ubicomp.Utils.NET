# MulticastTransportFramework

**MulticastTransportFramework** implements a higher-level, reliable messaging protocol over UDP multicast, focusing on security, ordering, and developer productivity.

## Architecture

*   **Fluent Builder**: Use `TransportBuilder` to configure and create a `TransportComponent`.
*   **Strongly-Typed Messaging**: Generic `SendAsync<T>` methods handle internal serialization and routing via `[MessageType("id")]` attributes.
*   **Message Processing pipeline**:
    *   **Binary Protocol**: Uses an optimized custom binary format (`BinaryPacket`) for efficiency.
    *   **GateKeeper**: Optional mechanism that ensures strictly ordered message processing using sequence IDs and PriorityQueues.
    *   **ReplayWindow**: Protects against replay attacks and duplicate messages using a sliding window.
*   **Reliability (ACKs)**: Supports acknowledgement-based sessions (`AckSession`) for reliable delivery of critical messages.
*   **Security**:
    *   **Confidentiality**: Built-in **AES-GCM** encryption (Modern Runtimes) or **AES-CBC** (Legacy Runtimes).
    *   **Integrity**: **HMAC-SHA256** signatures ensure packets are not tampered with.
    *   **Key Derivation**: Keys are derived from a shared secret using HKDF-like logic.
*   **Auto-Discovery**: Compatible with Roslyn Source Generators to automatically register message types decorated with the `[MessageType]` attribute.

## Diagrams
![MulticastTransportFramework Flow Diagram](assets/transport_flow_diagram.png)

## Core Logic
1.  **Incoming Data**: `MulticastSocket` receives bytes into a **pooled buffer**.
2.  **Consumption**: `TransportComponent` consumes messages from the socket's `IAsyncEnumerable<SocketMessage>` stream.
3.  **Security & Integrity**: Validates HMAC signature and decrypts payload using AES-GCM (or AES-CBC).
4.  **Deduplication**: `ReplayWindow` checks for duplicate or expired messages.
5.  **Ordering**: `GateKeeper` holds out-of-order messages until the gap is filled.
6.  **Dispatch**: Routes the deserialized POCO to registered handlers based on its Message Type ID.

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
var transport = new TransportBuilder()
    .WithMulticastOptions(MulticastSocketOptions.LocalNetwork())
    .WithLogging(loggerFactory)
    .WithSecurityKey("YourSharedSecretKey")
    .WithEncryption(true)
    .Build();

transport.RegisterHandler<SensorData>((data, context) =>
{
    Console.WriteLine($"Received: {data.Value} from {context.Source}");
});

await transport.StartAsync();
```

### 3. Sending Messages

```csharp
// Simple send (Encrypted & Binary Serialized)
await transport.SendAsync(new SensorData { Value = 25.5 });

// Send with acknowledgment request
await transport.SendAsync(new SensorData { Value = 25.5 }, new SendOptions { RequestAck = true });
```

## Dependencies
- `MulticastSocket`
- `System.Text.Json`
- `Microsoft.Extensions.Logging.Abstractions`