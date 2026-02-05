# SampleApp

A comprehensive console application demonstrating the capabilities of the **MulticastTransportFramework**. It acts as a reference implementation for configuring the transport, handling messages, and using advanced features like encryption and reliability.

## Features Demonstrated
*   **Configuration**: Loading settings from `appsettings.json` and command-line arguments.
*   **Secure Transport**: Using AES encryption and HMAC integrity.
*   **Reliability**: Sending messages with acknowledgement requests (`RequestAck`).
*   **Reactive Stream**: Consuming messages via Rx (`System.Reactive`).
*   **Peer Discovery**: visualizing active peers on the network.

## Usage

### Run with Default Settings
```bash
dotnet run --project SampleApp/Ubicomp.Utils.NET.SampleApp.csproj
```

### Command Line Arguments
*   `--no-wait`: Exit immediately after sending (useful for scripts).
*   `--key <string>`: Set the shared security key (overrides appsettings).
*   `--address <ip>`: Set multicast group address.
*   `--port <int>`: Set multicast port.
*   `--ttl <int>`: Set Time-To-Live.
*   `--interface <ip>`: Bind to a specific network interface.
*   `--local`: Enable loopback (receive own messages).
*   `--ack`: Send a test message requesting an acknowledgement.
*   `--no-encryption`: Disable encryption even if a key is provided.
*   `-v`, `--verbose`: Enable verbose logging.

### Example: Secure Chat
Run two instances in separate terminals to see them communicate.

**Instance 1:**
```bash
dotnet run --project SampleApp/Ubicomp.Utils.NET.SampleApp.csproj -- --key "Secret123" --local
```

**Instance 2:**
```bash
dotnet run --project SampleApp/Ubicomp.Utils.NET.SampleApp.csproj -- --key "Secret123" --local --ack
```
