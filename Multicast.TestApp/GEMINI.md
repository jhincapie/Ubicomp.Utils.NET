# Multicast.TestApp Context

## Purpose
A minimal test application using the `TransportComponent` from the `MulticastTransportFramework`.

## Use Cases
*   **Isolation Testing**: Debugging transport issues with minimal configuration.
*   **Interface Binding**: Verifying network interface selection.
*   **Basic Send/Receive**: Confirming basic message flow.

## Key Files
*   `Program.cs`: Sets up a `TransportComponent` using `TransportBuilder`, registers a handler for `MockMessage`, and sends a few test messages.
*   `MockMessage.cs`: A simple POCO used for testing.

## Do's and Don'ts
*   **Do** use this app for quick connectivity checks.
*   **Do** modify `Program.cs` to test specific network configurations (e.g., TTL, Interface).
*   **Don't** assume this tests the raw socket layer directly; it still goes through the `TransportComponent`. Use the `CLI` tool's `sniff` command for raw socket inspection.
