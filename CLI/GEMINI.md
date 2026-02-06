# CLI Context

## Overview
**Ubicomp.Utils.NET.CLI** is a console application providing diagnostic utilities for the multicast solution.

## Commands

### `check`
*   **Purpose**: Verifies that multicast traffic works on the host.
*   **Method**: `TransportComponent.VerifyNetworkingAsync()`.
    *   Sends a test message to itself (Loopback).
    *   Waits for receipt.
    *   Logs success/failure.

### `sniff`
*   **Purpose**: Packet capture and inspection tool.
*   **Method**:
    *   Starts a `TransportComponent`.
    *   Subscribes to `MessageStream`.
    *   Uses `TransportDiagnostics.DumpPacket(byte[])` to print hex output.
    *   Runs indefinitely (`Task.Delay(-1)`).

## Dependencies
*   **Target Framework**: `net10.0` (Note: Matches the `.csproj` configuration).
*   **Project**: `Ubicomp.Utils.NET.MulticastTransportFramework`

## Do's and Don'ts
*   **Do** use `check` to verify basic connectivity before debugging higher-level protocols.
*   **Do** use `sniff` to capture raw packets when verifying wire formats.
*   **Don't** rely on these tools for automated monitoring; they are designed for interactive diagnostics.
