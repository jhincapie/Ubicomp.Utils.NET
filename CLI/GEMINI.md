# CLI Context

## Module Purpose
**CLI** provides developer tools for diagnosing and monitoring the multicast environment.

## Key Components

### 1. `Program.cs`
*   **Entry Point**: Parses arguments and dispatches commands (`check`, `sniff`, `dashboard`).

### 2. `DashboardCommand`
*   **UI Framework**: Uses `Spectre.Console` for TUI layout (`Layout`, `Table`, `Panel`).
*   **Logic**: Starts a `TransportComponent`, verifies networking, and updates the display in a loop.
*   **Status**: Metrics and Logs panels are currently placeholders implementation-wise.

### 3. `TransportDiagnostics` (via Framework)
*   **Sniffer**: Uses `TransportDiagnostics.DumpPacket` to format binary payloads.

## Do's and Don'ts
*   **Do** use `check` before deploying to a new environment to verify firewall rules.
*   **Do** use `sniff` to debug serialization issues or inspect raw traffic.
