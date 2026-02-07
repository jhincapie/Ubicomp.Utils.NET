# Analyzers Context

## Purpose
Provides compile-time checks to ensure correct usage of the library, specifically regarding message type registration.

## Analyzers
*   **UBI001**: Checks that types passed to `TransportComponent.SendAsync<T>` have the `[MessageType]` attribute. This prevents runtime errors where the transport system doesn't know how to route a message.

## Do's and Don'ts
*   **Do** fix all errors reported by this analyzer.
*   **Do** ensure all message POCOs have `[MessageType("unique.id")]`.
