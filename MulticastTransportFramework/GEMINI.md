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
    *   **JSON**: Legacy support via `System.Text.Json` (replacing Newtonsoft for ~2x perf).
*   **Dispatch**: Routes messages to registered handlers based on string IDs defined in `[MessageType("id")]`.
*   **Auto-Discovery**: Source Generator (`Generators` project) automatically registers types with `[MessageType]` attribute during `Build()`.

### 2. Reliability & Integrity
*   **ReplayWindow**: A sliding window mechanism that rejects duplicate or replayed messages based on Sequence IDs and Timestamps. Uses a 64-bit mask.
*   **AckSession**: Provides "Reliable Multicast" semantics on a per-message basis. Senders can await explicit acknowledgements from peers.

### 3. Security
*   **Encryption**: **AES-GCM** (Authenticated Encryption).
*   **Integrity**: **HMAC-SHA256** signatures ensure packets are not tampered with.
*   **Key Derivation**: Keys are derived from a shared secret using HKDF-like logic.

## Key Classes
*   **`TransportBuilder`**: Fluent API for configuring reliability, security, and handlers. Uses Reflection/Generators for auto-discovery.
*   **`TransportComponent`**: The central actor managing the lifecycle and internal loops.
*   **`BinaryPacket`**: The wire-format structure.
*   **`AckSession`**: Manages state for pending acknowledgements.
*   **`NetworkDiagnostics`**: Static helper for firewall checks and loopback tests.

## Do's and Don'ts
*   **Do** use `TransportBuilder` to construct the component.
*   **Do** define message types as POCOs with the `[MessageType]` attribute.
*   **Don't** manually instantiate `TransportComponent` without the builder unless you are writing a custom factory.
*   **Don't** modify `BinaryPacket` structure without updating the corresponding tests and versioning logic.

## Dependencies
*   **Internal**: `MulticastSocket`
*   **External**: `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`
