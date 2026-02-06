# Multicast.TestApp Context

## Purpose
A minimal test application focusing solely on the `MulticastSocket` layer. It bypasses the higher-level Transport Framework.

## Use Cases
*   **Isolation Testing**: Debugging raw socket issues without the noise of the Transport Framework (GateKeeper, Serialization, etc.).
*   **Interface Binding**: Verifying `JoinGroup` behavior on specific network interfaces.
*   **Packet Inspection**: Verifying raw byte arrival.

## Key Files
*   `Program.cs`: Sets up a `MulticastSocket`, joins a group, and logs received packets.

## Do's and Don'ts
*   **Do** use this app for isolating network interface issues.
*   **Do** verify that `LocalIP` binding works as expected here before checking higher layers.
*   **Don't** add complex transport logic (ACKs, ordering) here; keep it raw.
