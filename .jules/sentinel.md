## 2026-02-05 - [Arbitrary Buffer Limits]
**Vulnerability:** `MulticastSocket` used a hardcoded internal buffer of 1024 bytes, causing silent truncation or dropping of UDP packets larger than 1KB.
**Learning:** Arbitrary application-layer buffer limits on transport protocols (UDP) can inadvertently create Availability (DoS) and Integrity issues by preventing standard protocol usage.
**Prevention:** Ensure transport buffers align with protocol specifications (e.g., UDP Max Size 65535) or are configurable.

## 2026-02-05 - [Log Injection via Resource Name]
**Vulnerability:** `TransportComponent` logged the `EventSource.ResourceName` from incoming ACK messages without sanitization, allowing attackers to forge log entries via newline injection (CWE-117).
**Learning:** Data received from the network (even metadata like source names) is untrusted and must be sanitized before being written to logs, especially when using structured logging that might be aggregated.
**Prevention:** Sanitize all user-controlled inputs before logging, specifically removing control characters like `\n` and `\r`.

## 2026-02-09 - [Unbounded Channel Memory Exhaustion]
**Vulnerability:** `MulticastSocket` used an internal unbounded `Channel<SocketMessage>` to buffer incoming UDP packets. A slow consumer or high-traffic flood could cause memory exhaustion (DoS).
**Learning:** Even low-level transport components must enforce backpressure or limits. `Channel.CreateUnbounded` is a dangerous default for network-facing buffers.
**Prevention:** Use `Channel.CreateBounded` with a sensible default limit and `DropWrite` (or other policy) to shed load gracefully under pressure.

## 2026-02-12 - [Replay Attack via Multicast]
**Vulnerability:** `TransportComponent` accepted all messages with valid timestamps but lacked a mechanism to detect and block duplicate `MessageId`s within the validity window.
**Learning:** Checking timestamps only ensures messages are recent, not unique. In multicast environments where packets can be duplicated or maliciously replayed, uniqueness checks are critical.
**Prevention:** Implement a sliding window deduplication cache using `MessageId` and expiration times to reject previously seen messages.
