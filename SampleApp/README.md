# SampleApp

The **Ubicomp.Utils.NET.SampleApp** demonstrates the usage of the `MulticastTransportFramework`. It can act as a sender or receiver, sending simple text messages and processing acknowledgements.

## Features
*   **TransportBuilder**: Configures the stack with encryption, logging, and handlers.
*   **Rx Integration**: Shows how to subscribe to `MessageStream` using Reactive Extensions.
*   **Peer Discovery**: Periodically prints active peers.
*   **Reliability**: Demonstrates sending with `RequestAck=true`.

## Usage
Run the app from the root directory:

```bash
dotnet run --project SampleApp/Ubicomp.Utils.NET.SampleApp.csproj -- [options]
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `--address <ip>` | Multicast Group IP | `239.0.0.1` |
| `--port <port>` | UDP Port | `5000` |
| `--ttl <value>` | Time-To-Live | `1` |
| `--interface <ip>` | Bind to a specific NIC | Auto |
| `--key <secret>` | Shared secret for encryption/integrity | None (Cleartext) |
| `--no-encryption` | Disable encryption (integrity only) if key is present | `false` |
| `--local` | Allow loopback (receive own messages) | `false` |
| `--ack` | Send a test message requesting ACKs | `false` |
| `--no-wait` | Exit after sending (don't block for ACKs) | `false` |
| `-v`, `--verbose` | Enable verbose logging | `false` |

### Examples

**1. Basic Receiver:**
```bash
dotnet run --project SampleApp/Ubicomp.Utils.NET.SampleApp.csproj
```

**2. Secure Sender (with Encryption):**
```bash
dotnet run --project SampleApp/Ubicomp.Utils.NET.SampleApp.csproj -- --key "MySecretKey" --ack
```

**3. Specific Interface:**
```bash
dotnet run --project SampleApp/Ubicomp.Utils.NET.SampleApp.csproj -- --interface 192.168.1.50
```
