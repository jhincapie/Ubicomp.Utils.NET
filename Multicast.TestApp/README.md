# Multicast.TestApp

A simple testbed application for validating the **MulticastTransportFramework** functionality in isolation.

## Purpose
Unlike the `SampleApp` (which demonstrates a full-featured scenario), this app focuses on a minimal setup of the `TransportComponent`. It is useful for:
*   Verifying multicast group joining on specific network interfaces.
*   Testing message sending and receiving with minimal configuration.
*   Debugging basic connectivity issues.

## Usage
```bash
dotnet run --project Multicast.TestApp/Multicast.TestApp.csproj
```
