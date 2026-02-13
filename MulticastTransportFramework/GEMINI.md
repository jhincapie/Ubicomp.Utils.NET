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
    *   **BinaryPacket**: Optimized format.
    *   **JSON**: Fallback. Uses `System.Text.Json` (v8.0).
    *   **Zero-Allocation**: Uses `ArrayBufferWriter<byte>` and `Span<byte>` where possible.

### 4. Auto-Discovery
*   **Source Generator**: Scans for `[MessageType]` attributes and generates a `RegisterDiscoveredMessages` extension method called by `TransportBuilder`.

## Binary Protocol (BinaryPacket)
The default wire format is a custom binary protocol designed for compactness.

**Structure:**
`[Magic:1][Version:1][Flags:1][NonceLen:1][TagLen:1][NameLen:1][Reserved:10][SeqId:4][MsgId:16][SourceId:16][Tick:8][TypeLen:1][Type:N][Name:K][Nonce:L][Tag:M][Payload:P]`

| Field | Size (Bytes) | Description |
| :--- | :--- | :--- |
| **Magic** | 1 | Protocol Magic Byte (`0xAA`) |
| **Version** | 1 | Protocol Version (`1`) |
| **Flags** | 1 | Bitmask (0x1: RequestAck, 0x2: Encrypted) |
| **NonceLen** | 1 | Length of Nonce (12 for AES-GCM, 0 otherwise) |
| **TagLen** | 1 | Length of Auth Tag (16 for AES-GCM, 32 for HMAC) |
| **NameLen** | 1 | Length of Sender Name |
| **Reserved** | 10 | Reserved for future use (zeroed) |
| **SeqId** | 4 | Sender Sequence Number (Int32) |
| **MsgId** | 16 | Message GUID |
| **SourceId** | 16 | Source GUID |
| **Tick** | 8 | Timestamp (Ticks) |
| **TypeLen** | 1 | Length of Message Type string |
| **Type** | N | Message Type (UTF-8) |
| **Name** | K | Sender Name (UTF-8) |
| **Nonce** | L | IV/Nonce (if Encrypted) |
| **Tag** | M | Auth Tag / HMAC |
| **Payload** | P | Serialized Message Body (JSON/Binary) |

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
