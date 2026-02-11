# Tests

The **Tests** project contains comprehensive unit and integration tests for the entire `Ubicomp.Utils.NET` solution.

## Structure

*   **Components/**: Unit tests for internal components (`AckManager`, `ReplayProtector`, `PeerManager`).
*   **Integration**: End-to-end tests verifying `TransportComponent` and `MulticastSocket` interactions.
    *   `MulticastSocketTests.cs`: Low-level socket behavior.
    *   `TransportComponentTests.cs`: High-level messaging and handlers.
*   **Security**: Verification of Encryption (`EncryptionTests.cs`) and Integrity (`IntegrityTests.cs`).

## Key Test Classes

### `MulticastSocketTests`
*   Verifies binding, joining groups, and sending/receiving bytes.
*   Includes `FirewallCheck` diagnostic test.

### `TransportComponentTests`
*   Verifies the full pipeline: Serialize -> Send -> Receive -> Deserialize -> Dispatch.
*   Tests `[MessageType]` routing.

### `InMemorySocketTests`
*   Uses `InMemoryMulticastSocket` to test logic without touching the OS network stack.

## Running Tests
Run all tests from the repository root:

```bash
dotnet test Tests/Ubicomp.Utils.NET.Tests.csproj
```

**Run a specific test:**
```bash
dotnet test Tests/Ubicomp.Utils.NET.Tests.csproj --filter "FullyQualifiedName~TransportComponentTests"
```
