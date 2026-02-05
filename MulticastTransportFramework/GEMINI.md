# MulticastTransportFramework Context

## Architecture
**MulticastTransportFramework** implements a higher-level, reliable messaging protocol over UDP multicast.

![Transport Flow Diagram](assets/transport_flow_diagram.png)

## Core Logic & Features

### 1. Message Processing Pipeline
*   **Source**: Consumes `IAsyncEnumerable<SocketMessage>` from `MulticastSocket`.
*   **Deserialization**: Supports two modes:
    *   **BinaryPacket**: Optimized, custom binary protocol with security headers.
        *   **Reduced Overhead**: Payload size reduced by ~66% compared to legacy JSON envelope.
        *   **Compatibility**: Falls back to legacy JSON format if `Magic Byte` is not present, ensuring backward compatibility.
    *   **JSON**: Legacy support via `Newtonsoft.Json`.
*   **GateKeeper**: A priority-queue based mechanism that enforces strictly ordered message processing (`EnforceOrdering` option). It holds out-of-order messages until the gap is filled.
*   **Dispatch**: Routes messages to registered handlers based on string IDs defined in `[MessageType("id")]`.
*   **Auto-Discovery**: Source Generator automatically registers types with `[MessageType]` attribute (typically during `Build()`).

### 2. Reliability & Integrity
*   **ReplayWindow**: A sliding window mechanism that rejects duplicate or replayed messages based on Sequence IDs and Timestamps.
*   **AckSession**: Provides "Reliable Multicast" semantics on a per-message basis. Senders can await explicit acknowledgements from peers.

### 3. Security
*   **Encryption**: **AES-GCM** (primary) or AES-CBC (fallback) for payload confidentiality.
    *   **Modern Runtimes**: Uses `System.Security.Cryptography.AesGcm` (standard 2.1+) for authenticated encryption.
    *   **Legacy Runtimes**: Falls back to `Aes` (CBC Mode) + HMAC for standard 2.0.
*   **Integrity**: **HMAC-SHA256** signatures ensure packets are not tampered with.
*   **Key Derivation**: Keys are derived from a shared secret using HKDF-like logic.

## Key Classes
*   **`TransportBuilder`**: Fluent API for configuring reliability, security, and handlers.
*   **`TransportComponent`**: The central actor managing the lifecycle and internal loops.
*   **`BinaryPacket`**: The wire-format structure.
*   **`AckSession`**: Manages state for pending acknowledgements.

## Dependencies
*   **Internal**: `MulticastSocket`
*   **External**: `Newtonsoft.Json`, `Microsoft.Extensions.Logging.Abstractions`