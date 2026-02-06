# Ubicomp.Utils.NET CLI

A command-line tool for managing, diagnosing, and inspecting the multicast environment.

## Installation
The tool is built as part of the solution.

```bash
dotnet build CLI/Ubicomp.Utils.NET.CLI.csproj
```

## Usage

Run the tool using `dotnet run`:

```bash
dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- <command> [options]
```

### Commands

#### `check`
Runs a suite of network diagnostic tests to verify multicast functionality.
*   **Checks**: Firewall status, Loopback capability.
*   **Usage**:
    ```bash
    dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- check
    ```

#### `sniff`
Listens for multicast packets on the default group and dumps their contents to the console.
*   **Usage**:
    ```bash
    dotnet run --project CLI/Ubicomp.Utils.NET.CLI.csproj -- sniff
    ```
*   **Output**: Displays Source IP, Sequence ID, and a hex dump of the packet payload.

## Dependencies
*   `Ubicomp.Utils.NET.MulticastTransportFramework`
*   `Microsoft.Extensions.Logging.Console`
