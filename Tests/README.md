# Ubicomp.Utils.NET Tests

This project contains the unit tests for the Ubicomp.Utils.NET solution.

## Running Tests
You can run the tests using the .NET CLI from the solution root:

```bash
dotnet test Tests/Ubicomp.Utils.NET.Tests.csproj
```

## Scope
The tests and core libraries target **.NET 8.0**.
*   **Serialization**: Verifies that `TransportMessage` objects (including polymorphic content) are correctly serialized/deserialized using both `Newtonsoft.Json` (Legacy) and `System.Text.Json` (Modern).
*   **MulticastSocket**: Verification of socket options, buffer management, and async streaming.
    *   *Note*: `NetworkChange` tests use Reflection to simulate system events.
*   **TransportFramework**: End-to-end flow of message creation, security (Encryption/Integrity), and ordering (GateKeeper).
*   **Generators**: Verifies that `[MessageType]` attributes are correctly discovered.

## Configuration
Dependencies include:
*   `Newtonsoft.Json`
*   `Microsoft.NET.Test.Sdk`
*   `xunit`
*   `xunit.runner.visualstudio`
