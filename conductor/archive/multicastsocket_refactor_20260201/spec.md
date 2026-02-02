# Specification: MulticastSocket Parametrization Refactor

## Overview
This track aims to refactor the `MulticastSocket` class to improve its flexibility and extensibility. We will move away from fixed constructor parameters towards a modern **Options Pattern** using a dedicated `MulticastSocketOptions` class. This allows specialized users to fine-tune low-level socket behavior while maintaining sensible defaults for standard use cases.

## Functional Requirements
- **Options Pattern Implementation**: Create a `MulticastSocketOptions` class that encapsulates all configuration settings.
- **Enhanced Configuration**:
    - **Advanced Socket Options**: Support for `ReuseAddress`, `MulticastLoopback`, `NoDelay`, and `DontFragment`.
    - **Buffer Management**: Configurable `ReceiveBufferSize` and `SendBufferSize`.
    - **Interface Filtering**: Support for specifying a list of target `IPAddress`es to bind/join, or a predicate for filtering.
    - **Lifecycle Control**: Options to enable/disable automatic joining upon instantiation.
- **Refactored Instantiation**: Update `MulticastSocket` to accept `MulticastSocketOptions`.
- **Backward Compatibility**: Provide a way to maintain (or easily migrate) existing constructor usage.

## Non-Functional Requirements
- **Performance**: Ensure that the overhead of using the Options pattern is negligible compared to socket I/O.
- **Reliability**: Maintain current robustness in joining multiple interfaces, especially when some interfaces fail to join.
- **Usability**: Sensible defaults should allow the socket to work out-of-the-box for most users.

## Documentation Requirements
- **Architectural Diagrams**: Update `MulticastSocket/assets/class_diagram.png` and the root `assets/system_architecture_diagram.png`.
- **User Documentation**: Update `MulticastSocket/README.md` with usage examples and a list of available options.
- **Code Documentation**: Full XML comments for all new public members.

## Acceptance Criteria
- [ ] `MulticastSocket` can be instantiated using `MulticastSocketOptions`.
- [ ] All configured socket options (TTL, Loopback, Buffers, etc.) are correctly applied to the underlying `Socket`.
- [ ] Interface filtering correctly limits joins to the specified subset of local IPs.
- [ ] Unit tests verify both default behavior and custom option configurations.
- [ ] Documentation (README and Diagrams) is updated to reflect the changes.

## Out of Scope
- Migrating the `MulticastTransportFramework` to the new Options pattern (this track focuses strictly on the `MulticastSocket` layer).
- Implementing IPv6 support.
