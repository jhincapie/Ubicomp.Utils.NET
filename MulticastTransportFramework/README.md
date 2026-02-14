# MulticastTransportFramework

**MulticastTransportFramework** implements a higher-level, reliable messaging protocol over UDP multicast. It provides a robust Actor-Model-like communication layer, targeting **.NET 8.0**, with features for reliability, security, and replay protection.

## Key Features

*   **Reliable Delivery**: Built-in acknowledgement (ACK) system (`AckSession`) for critical message delivery.
*   **Replay Protection**: `ReplayProtector` protects against replay attacks and deduplicates messages within a sliding window.
*   **Security**: Zero-conf encryption (**AES-GCM**) and integrity signing (**HMAC-SHA256**) derived from a shared secret. Also neutralizes Log Injection attacks by sanitizing inputs.
*   **Optimized Protocol**: Uses a custom `BinaryPacket` format for reduced overhead (~66% smaller than JSON), with fallback to JSON for legacy clients.
*   **Auto-Discovery**: Leverages Roslyn Source Generators to automatically register `[MessageType]` classes.
*   **Peer Discovery**: Automatic peer detection and tracking via heartbeats (`PeerManager`).
*   **Diagnostic Tools**: Built-in network verification and firewall checks (`NetworkDiagnostics`).

## Installation
The library is part of the `Ubicomp.Utils.NET` solution. Ensure your project targets `.NET 8.0`.

## Usage

### 1. Define Message Types
Create a POCO and decorate it with the `[MessageType]` attribute. This ID is used for routing.

```csharp
using Ubicomp.Utils.NET.MulticastTransportFramework;

[MessageType("app.greeting")]
public class GreetingMessage
{
    public string Text { get; set; } = string.Empty;
    public int Priority { get; set; }
}
```

### 2. Configure and Start
Use the `TransportBuilder` to create and start the component.

```csharp
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;

var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5000);

var transport = new TransportBuilder()
    .WithMulticastOptions(options)
    .WithLocalSource("MyDevice")
    .WithHeartbeat(TimeSpan.FromSeconds(5)) // Optional: Set heartbeat interval
    .WithInstanceMetadata("Version=1.0;Env=Prod") // Optional: Share metadata with peers
    .WithLogging(loggerFactory)
    .WithSecurityKey("SuperSecretKey123!")  // Enables HMAC integrity & Encryption
    .WithEncryption(true)                   // Enables AES-GCM payload encryption
    .RegisterHandler<GreetingMessage>((msg, context) =>
    {
        Console.WriteLine($"Received: {msg.Text} from {context.Source.ResourceName}");
    })
    .Build();

transport.Start();
```

> **Note**: `TransportBuilder.Build()` automatically scans your assembly for `[MessageType]` attributes and registers them using a Source Generator.

### 3. Send a Message
Sending is asynchronous and supports reliability options.

```csharp
// Simple Send (Fire-and-forget)
await transport.SendAsync(new GreetingMessage { Text = "Hello!" });

// Reliable Send (Request ACK)
var options = new SendOptions { RequestAck = true, AckTimeout = TimeSpan.FromSeconds(2) };
var session = await transport.SendAsync(new GreetingMessage { Text = "Important!" }, options);

// Wait for ACKs
bool success = await session.WaitAsync(TimeSpan.FromSeconds(2));
if (success)
{
    Console.WriteLine($"Acknowledged by {session.ReceivedAcks.Count} peers.");
}
```

## Architecture

### Components
*   **`TransportComponent`**: The central facade. Manages the lifecycle and delegates to internal components.
*   **`ReplayProtector`**: Validates message sequence numbers to prevent replay attacks and handles deduplication.
*   **`AckManager`**: Handles reliable delivery sessions (`AckSession`) and automatic ACK responses.
*   **`PeerManager`**: Tracks active peers via `HeartbeatMessage` and exposes `ActivePeers` property.
*   **`SecurityHandler`**: Manages encryption/decryption, key rotation (`RekeyMessage`), and log sanitization.
*   **`MessageSerializer`**: Handles `BinaryPacket` (primary) and JSON (fallback) serialization.

### Protocol
The framework uses a dual-protocol approach:
1.  **BinaryPacket**: Default. Compact binary header + payload.
2.  **JSON**: Legacy fallback. Standard JSON envelope.

## Diagnostics
Use `NetworkDiagnostics` to verify multicast connectivity.

```csharp
bool isOk = await transport.VerifyNetworkingAsync();
if (!isOk) Console.WriteLine("Multicast loopback failed!");
```
