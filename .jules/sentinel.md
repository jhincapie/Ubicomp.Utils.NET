## 2026-02-05 - [Arbitrary Buffer Limits]
**Vulnerability:** `MulticastSocket` used a hardcoded internal buffer of 1024 bytes, causing silent truncation or dropping of UDP packets larger than 1KB.
**Learning:** Arbitrary application-layer buffer limits on transport protocols (UDP) can inadvertently create Availability (DoS) and Integrity issues by preventing standard protocol usage.
**Prevention:** Ensure transport buffers align with protocol specifications (e.g., UDP Max Size 65535) or are configurable.

## 2026-02-05 - [Log Injection via Resource Name]
**Vulnerability:** `TransportComponent` logged the `EventSource.ResourceName` from incoming ACK messages without sanitization, allowing attackers to forge log entries via newline injection (CWE-117).
**Learning:** Data received from the network (even metadata like source names) is untrusted and must be sanitized before being written to logs, especially when using structured logging that might be aggregated.
**Prevention:** Sanitize all user-controlled inputs before logging, specifically removing control characters like `\n` and `\r`.
