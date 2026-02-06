# Tests Context

## Scope
Comprehensive unit and integration tests for the entire solution.

## Key Test Suites
*   **`MulticastSocketTests`**: Socket options, buffer pooling, streaming.
*   **`TransportComponentTests`**: Security (Encryption/HMAC), Ordering (GateKeeper), AckSession.
*   **`ContextServiceTests`**: MSE pattern validation.
*   **`GeneratorsTests`**: Verifying Source Generator output.

## Frameworks
*   **xUnit**: Test runner.
*   **Mocking**: Manual test doubles (e.g., `InMemoryMulticastSocket`) are preferred over mocking frameworks.

## Do's and Don'ts
*   **Do** use `InMemoryMulticastSocket` for testing transport logic without network I/O.
*   **Do** write unit tests for every new message type or handler.
*   **Don't** introduce Flaky tests that depend on real wall-clock timing; use deterministic coordination where possible.
