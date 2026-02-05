## 2026-02-05 - [Arbitrary Buffer Limits]
**Vulnerability:** `MulticastSocket` used a hardcoded internal buffer of 1024 bytes, causing silent truncation or dropping of UDP packets larger than 1KB.
**Learning:** Arbitrary application-layer buffer limits on transport protocols (UDP) can inadvertently create Availability (DoS) and Integrity issues by preventing standard protocol usage.
**Prevention:** Ensure transport buffers align with protocol specifications (e.g., UDP Max Size 65535) or are configurable.
