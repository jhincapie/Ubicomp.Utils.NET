# Ubicomp.Utils.NET.CLI

A command-line tool for diagnosing and inspecting multicast network traffic using the **MulticastSocket** and **MulticastTransportFramework**.

## Target Framework
This tool targets **.NET 8.0**.

## Installation & Usage
You can run the tool directly from source:

```bash
dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- <command>
```

## Commands

### `check`
Runs a suite of network diagnostics, including firewall checks and loopback tests, to verify that the environment is correctly configured for multicast.

```bash
dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- check
```

### `sniff`
Listens for multicast packets on the default group (239.0.0.1:5000) and prints their raw contents (hex dump and parsed structure) to the console. Useful for verifying that packets are arriving on the wire.

```bash
dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- sniff
```

### `dashboard`
Launches an interactive, live terminal dashboard (using Spectre.Console) that displays:
*   Transport Status
*   Active Peers
*   Live Logs
*   Metrics

```bash
dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- dashboard
```

## Troubleshooting
If `check` fails or `sniff` sees no packets:
1.  **Firewall**: Ensure UDP port 5000 is open for inbound/outbound traffic.
2.  **Interface**: The tool binds to `IPAddress.Any` by default. If you have multiple NICs (e.g., VPN, VirtualBox), multicast routing might be confused. You may need to disable virtual adapters temporarily.
