# Multicast.TestApp

A simple testbed application for validating the low-level **MulticastSocket** library functionality in isolation.

## Purpose
Unlike the `SampleApp` (which uses the full Transport Framework), this app focuses on the raw socket layer. It is useful for:
*   Verifying multicast group joining on specific network interfaces.
*   Testing socket options (TTL, Loopback).
*   Debugging raw packet reception issues.

## Usage
```bash
dotnet run --project Multicast.TestApp/Multicast.TestApp.csproj
```
