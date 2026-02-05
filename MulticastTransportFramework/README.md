# MulticastTransportFramework

**MulticastTransportFramework** implements a higher-level, reliable messaging protocol over UDP multicast, focusing on security, ordering, and developer productivity. It provides a robust Actor-Model-like communication layer for distributed local network applications.

## Key Features

*   **Reliable Delivery**: Built-in acknowledgement (ACK) system with optional automatic responses.
*   **Ordered Processing**: "GateKeeper" mechanism ensures strict message sequencing using PriorityQueues, correcting UDP out-of-order delivery.
*   **Security**: Zero-conf encryption (AES-GCM/AES-CBC) and integrity signing (HMAC-SHA256) derived from a shared secret.
*   **Peer Discovery**: Automatic peer detection and tracking via heartbeats.
*   **Developer Friendly**: Strongly-typed message routing and a fluent Builder API.
*   **Diagnostic Tools**: Built-in network verification and firewall checks.

## Quick Start

### 1. Define a Message
Create a POCO and decorate it with the `[MessageType]` attribute. This ID is used for routing.

```csharp
using Ubicomp.Utils.NET.MulticastTransportFramework;

[MessageType("app.greeting")]
public class GreetingMessage
{
    public string Text { get; set; }
    public int Priority { get; set; }
}
```

### 2. Configure and Start
Use the `TransportBuilder` to create and start the component.

```csharp
var transport = new TransportBuilder()
    .WithMulticastOptions(MulticastSocketOptions.Default)
    .WithSecurityKey("SuperSecretSharedKey123!") // Enables HMAC integrity
    .WithEncryption(true)                         // Enables AES payload encryption
    .RegisterHandler<GreetingMessage>((msg, context) =>
    {
        Console.WriteLine($"Received from {context.Source.ResourceName}: {msg.Text}");
    })
    .Build();

transport.Start();
```

### 3. Send a Message
```csharp
await transport.SendAsync(new GreetingMessage
{
    Text = "Hello World!",
    Priority = 1
});
```

---

## Configuration

The `TransportBuilder` provides a fluent API for all configuration needs.

| Method | Description |
|--------|-------------|
| `WithMulticastOptions(options)` | Sets the underlying socket options (Group IP, Port, TTL). |
| `WithSecurityKey(key)` | Sets the shared secret. Required for integrity signing and encryption. |
| `WithEncryption(bool)` | Enables AES payload encryption. Requires `WithSecurityKey`. |
| `WithEnforceOrdering(bool)` | Enables strict sequencing (GateKeeper). |
| `WithAutoSendAcks(bool)` | Automatically replies to messages requesting ACKs. |
| `WithHeartbeat(TimeSpan)` | Enables periodic heartbeats for peer discovery. |
| `WithInstanceMetadata(string)` | Sets custom metadata broadcast in heartbeats. |
| `WithLogging(ILoggerFactory)` | Connects the internal logger. |
| `WithLocalSource(name, id)` | Sets the identity of this node. |

---

## Core Features in Depth

### 1. Ordering (GateKeeper)
UDP packets often arrive out of order. The framework can enforce strict sequencing.

*   **How it works**: Incoming messages are assigned a sequence ID. If a gap is detected (e.g., received 1, then 3), message 3 is held in a `PriorityQueue`.
*   **Configuration**:
    ```csharp
    .WithEnforceOrdering(true)
    ```
*   **Behavior**: If the missing packet (2) does not arrive within `GateKeeperTimeout` (default 500ms), the system logs a warning and processes the next available message to prevent stalling.

### 2. Security (Encryption & Integrity)
Security is handled transparently. Keys are derived from the `SecurityKey` using HKDF (HMAC-SHA256).

*   **Integrity**: All messages are signed with HMAC-SHA256 (if key is present) or SHA256 (if no key). Unsigned or invalid messages are dropped.
*   **Encryption**:
    *   **Modern Runtimes (.NET Core 3.0+)**: Uses **AES-GCM** (Hardware Accelerated) for authenticated encryption.
    *   **Legacy Runtimes (.NET Standard 2.0)**: Falls back to **AES-CBC** with PKCS7 padding.
*   **Usage**:
    ```csharp
    .WithSecurityKey("My_Shared_Secret_Passphrase")
    .WithEncryption(true)
    ```

### 3. Reliability (Acknowledgements)
You can request receipt confirmation for critical messages.

**Manual Request & Handling:**
```csharp
var options = new SendOptions { RequestAck = true, AckTimeout = TimeSpan.FromSeconds(2) };
var session = await transport.SendAsync(new CriticalMessage(), options);

// Wait for ACKs
bool receivedAny = await session.WaitAsync(TimeSpan.FromSeconds(2));

// Inspect individual ACKs
foreach(var source in session.ReceivedAcks)
{
    Console.WriteLine($"Acknowledged by {source.ResourceName}");
}
```

**Automatic Responses:**
Enable `WithAutoSendAcks(true)` on the receiver to automatically reply with a system ACK (`sys.ack`) whenever a message with `RequestAck=true` is successfully processed.

### 4. Peer Discovery
The framework can track other active nodes on the multicast group.

*   **Setup**:
    ```csharp
    .WithHeartbeat(TimeSpan.FromSeconds(5))
    .WithInstanceMetadata("{\"role\": \"server\"}")
    ```
*   **Accessing Peers**:
    ```csharp
    // Real-time access
    foreach(var peer in transport.ActivePeers)
    {
        Console.WriteLine($"Peer: {peer.DeviceName} (Last Seen: {peer.LastSeen})");
    }

    // Events
    transport.OnPeerDiscovered += peer => Console.WriteLine($"New Peer: {peer.DeviceName}");
    transport.OnPeerLost += peer => Console.WriteLine($"Peer Lost: {peer.DeviceName}");
    ```

### 5. Network Diagnostics
Multicast can be tricky due to firewalls. The framework includes diagnostic tools.

```csharp
// Check firewall rules (logs to configured logger)
NetworkDiagnostics.LogFirewallStatus(5000, logger);

// Perform a loopback test (sends a message to self)
bool loopbackOk = await NetworkDiagnostics.PerformLoopbackTestAsync(transport);

if (!loopbackOk)
{
    Console.WriteLine("WARNING: Multicast loopback failed. Check firewall.");
}
```

## Architecture

*   **TransportComponent**: The main entry point. Manages the socket, serialization, and actor loops.
*   **GateKeeper**: An internal actor loop that manages the `PriorityQueue` for ordering.
*   **AckSession**: Tracks acknowledgements for specific message IDs.
*   **PeerTable**: Manages the list of active peers and handles expiration.

![MulticastTransportFramework Flow Diagram](assets/transport_flow_diagram.png)
