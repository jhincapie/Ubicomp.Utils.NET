# MulticastTransportFramework Context

## Module Purpose
**MulticastTransportFramework** provides a high-level, reliable, and secure messaging layer over raw UDP multicast. It abstracts the complexities of packet loss, ordering, and security.

## Architectural Context
*   **Layer**: Middle layer (Transport & Reliability).
*   **Used By**: `ContextAwarenessFramework` (optional integration), Applications.
*   **Dependencies**: `MulticastSocket`, `System.Text.Json`, `Microsoft.Extensions.Logging`.

## Key Components

### 1. `TransportComponent` (Facade)
*   **Role**: Main entry point.
*   **Responsibilities**:
    *   Lifecycle management (`Start`, `Stop`).
    *   Message Dispatch (`RegisterHandler`, `SendAsync`).
    *   Integration of sub-components (`AckManager`, `PeerManager`, etc.).

### 2. Internal Components
*   **`ReplayProtector`**:
    *   **Function**: Prevents replay attacks and handles message deduplication.
    *   **Mechanism**: Sliding window of sequence IDs + Timestamp validity check (5 min window).
*   **`AckManager`**:
    *   **Function**: Manages reliability.
    *   **Mechanism**: Creates `AckSession` for outgoing messages with `RequestAck=true`. Automatically replies with `sys.ack` for incoming requests.
*   **`PeerManager`**:
    *   **Function**: Discovers peers.
    *   **Mechanism**: Broadcasts periodic `HeartbeatMessage` (ID: `sys.heartbeat`). Maintains a `PeerTable`.
*   **`SecurityHandler`**:
    *   **Function**: Encryption, Integrity, and Log Sanitization.
    *   **Mechanism**: AES-GCM (Encryption), HMAC-SHA256 (Integrity). Supports key rotation via `RekeyMessage`.
    *   **Log Security**: Implements `SanitizeLog` to neutralize Log Injection (CWE-117) by replacing control characters.

### 3. Serialization
*   **`MessageSerializer`**:
    *   **BinaryPacket**: Optimized format. `MagicByte` -> `Header` -> `Payload`.
    *   **JSON**: Fallback. Uses `System.Text.Json` (v8.0).
    *   **Zero-Allocation**: Uses `ArrayBufferWriter<byte>` and `Span<byte>` where possible.

### 4. Auto-Discovery
*   **Source Generator**: Scans for `[MessageType]` attributes and generates a `RegisterDiscoveredMessages` extension method called by `TransportBuilder`.

## Do's and Don'ts

### Do's
*   **Do** use `TransportBuilder` for configuration.
*   **Do** decorate message POCOs with `[MessageType("unique.id")]`.
*   **Do** use `SendOptions` to request ACKs for critical messages.
*   **Do** rely on `ActivePeers` for network visibility.

### Don'ts
*   **Don't** use `TransportComponent` constructor directly; use the builder.
*   **Don't** assume messages arrive strictly in order; the transport layer handles replay protection but not reordering.
*   **Don't** share `SecurityKey` in insecure channels.

## File Structure
*   `TransportComponent.cs`: Main class.
*   `TransportBuilder.cs`: Fluent configuration.
*   `TransportMessage.cs`: Message envelope.
*   `Components/`: Internal logic (`AckManager`, `PeerManager`, `ReplayProtector`, `SecurityHandler`, `MessageSerializer`).
*   `BinaryPacket.cs`: Protocol definition.
