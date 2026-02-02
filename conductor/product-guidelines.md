# Product Guidelines

## Documentation Style
- **Concise and Minimalist**: Documentation should be brief and direct. Prioritize technical accuracy and code snippets over long explanatory prose. 
- **Code-First**: Use self-contained snippets to demonstrate functionality. If a concept can be explained with code, prefer that over text.

## Architectural Principles
- **Modularity**: Components must remain decoupled. Users should be able to utilize low-level libraries (like `MulticastSocket`) without being forced into higher-level frameworks.
- **Event-Driven Communication**: Use C# events and delegates as the primary mechanism for cross-component communication and data updates to maintain responsiveness and decoupling.
- **Internal Thread Safety**: Libraries must handle internal threading concerns (e.g., background network loops vs. UI thread dispatching) to provide a seamless experience for the developer.

## Design Patterns
- **Monitor-Service-Entity**: Follow the established pattern in the `ContextAwarenessFramework` for consistency.
- **Singleton Orchestration**: Use singletons (like `TransportComponent.Instance`) for central managers while allowing for dependency injection where appropriate.
- **Template Method for Persistence**: Provide abstract hooks for persistence logic to allow for modular storage implementations.
