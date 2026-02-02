# Implementation Plan: MulticastSocket Parametrization Refactor

This plan outlines the steps to refactor `MulticastSocket` to use the Options pattern, improving flexibility and testability.

## Phase 1: Foundation and Options Definition
Focus on defining the new configuration structure and setting up the testing environment.

- [x] Task: Create `MulticastSocketOptions` class
    - [x] Define properties for `TargetIP`, `TargetPort`, `TimeToLive`, `LocalIP`.
    - [x] Add advanced properties: `ReuseAddress`, `MulticastLoopback`, `NoDelay`, `ReceiveBufferSize`, `SendBufferSize`.
    - [x] Implement default values matching current `MulticastSocket` behavior.
- [x] Task: Initialize Refactor Test Suite
    - [x] Create `Tests/MulticastSocketOptionsTests.cs`.
    - [x] Write tests to verify default values of `MulticastSocketOptions`.
- [x] Task: Conductor - User Manual Verification 'Phase 1: Foundation' (Protocol in workflow.md)

## Phase 2: Core Refactor and Integration
Refactor `MulticastSocket` to accept the new options while maintaining basic functionality.

- [x] Task: Implement Options-based Constructor in `MulticastSocket`
    - [x] **Write Tests**: Create failing tests in `Tests/MulticastSocketTests.cs` that attempt to instantiate the socket using `MulticastSocketOptions`.
    - [x] **Implement**: Add the new constructor and update internal logic to use `_options` instead of individual fields.
    - [x] **Verify**: Ensure existing tests still pass (backward compatibility or migration).
- [x] Task: Refactor Socket Setup Logic
    - [x] **Write Tests**: Write tests verifying that `TimeToLive` and `ReuseAddress` from options are applied to the underlying socket.
    - [x] **Implement**: Update `SetupSocket` and `SetDefaultSocketOptions` to pull values from the `MulticastSocketOptions` instance.
- [ ] Task: Conductor - User Manual Verification 'Phase 2: Core Refactor' (Protocol in workflow.md)

## Phase 3: Advanced Features and Filtering
Implement the more complex parametrization features like buffer tuning and interface filtering.

- [x] Task: Implement Buffer Size Customization
    - [x] **Write Tests**: Write tests that verify `ReceiveBufferSize` and `SendBufferSize` are correctly set on the `_udpSocket`.
    - [x] **Implement**: Add logic to `SetupSocket` to apply these buffer sizes if they are explicitly set in options.
- [x] Task: Enhance Interface Selection and Filtering
    - [x] **Write Tests**: Write tests that mock/simulate multiple interfaces and verify that only a specified subset is joined when a filter is provided.
    - [x] **Implement**: Refactor `JoinAllInterfaces` and `JoinSpecificInterface` to respect a new `InterfaceFilter` property in `MulticastSocketOptions`.
- [ ] Task: Conductor - User Manual Verification 'Phase 3: Advanced Features' (Protocol in workflow.md)

## Phase 4: Documentation and Cleanup
Finalize the track by updating all external documentation and diagrams.

- [x] Task: Update Technical Documentation
    - [x] Update XML comments for all new members in `MulticastSocketOptions`.
    - [x] Update `MulticastSocket/README.md` with "Advanced Configuration" examples.
- [x] Task: Update Architectural Diagrams
    - [x] Update `MulticastSocket/assets/class_diagram.png` (requires manual/tool update).
    - [x] Update `assets/system_architecture_diagram.png` to show the new Options dependency.
- [ ] Task: Conductor - User Manual Verification 'Phase 4: Documentation' (Protocol in workflow.md)
