# Ubicomp.Utils.NET Tests

This project contains the unit tests for the Ubicomp.Utils.NET solution.

## Running Tests
You can run the tests using the .NET CLI from the solution root or the `Tests` directory:

```bash
dotnet test
```

## Coverage
The tests currently cover:
*   **Serialization**: Verifies that `TransportMessage` objects (including polymorphic content) are correctly serialized and deserialized using `Newtonsoft.Json`.
*   **MulticastSocket**: Basic checks for socket initialization and configuration (integration tests may require network access).
*   **TransportFramework**: Tests the end-to-end flow of message creation, serialization, and type resolution.

## Configuration
The tests target `.NET 8.0` and use `xUnit` as the testing framework.
Dependencies include:
*   `Newtonsoft.Json`
*   `Microsoft.NET.Test.Sdk`
*   `xunit`
