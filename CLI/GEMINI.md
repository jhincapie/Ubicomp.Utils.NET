# CLI Context

## Purpose
Diagnostic and inspection tools for the multicast environment.

## Target Framework
**.NET 8.0**

## Key Commands
*   **`check`**: Validates networking (loopback, firewall). Uses `TransportComponent.VerifyNetworkingAsync()`.
*   **`sniff`**: Raw packet capture and hex dump. Helpful for debugging low-level wire issues.
*   **`dashboard`**: TUI (Text User Interface) for monitoring peer status and transport metrics in real-time. Uses `Spectre.Console`.

## Do's and Don'ts
*   **Do** use `check` first when troubleshooting connectivity issues.
*   **Do** use `sniff` to verify if packets are physically reaching the machine.
*   **Don't** assume the CLI has access to application-specific keys; it uses default settings unless configured otherwise.
