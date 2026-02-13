# CLI Tools

The **Ubicomp.Utils.NET.CLI** project provides command-line utilities for diagnostics, monitoring, and debugging of the multicast network.

## Installation
Build and run directly from the repository root:

```bash
dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- [command]
```

## Commands

### 1. `check`
Runs a suite of network diagnostics to verify multicast connectivity.
*   **Checks**: Firewall rules, Multicast Loopback support.
*   **Usage**:
    ```bash
    dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- check
    ```

### 2. `sniff`
Listens for multicast packets on the default group (`239.0.0.1:5000`) and dumps their contents to the console.
*   **Usage**:
    ```bash
    dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- sniff
    ```

### 3. `dashboard`
Launches an interactive TUI (Text User Interface) dashboard using `Spectre.Console`.
*   **Features**:
    *   **Live Peer List**: Shows active peers discovered via heartbeats.
    *   **Transport Status**: Displays local address, port, and state.
    *   **Metrics**: (Coming Soon) Real-time throughput and packet counters.
    *   **Logs**: (Coming Soon) Live log stream.
*   **Usage**:
    ```bash
    dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- dashboard
    ```

## Dependencies
*   `Spectre.Console`: For rich terminal UI.
*   `MulticastTransportFramework`: Core networking logic.
