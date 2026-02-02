# Implementation Plan: Message Acknowledgement (Ack) Support

This plan outlines the steps to implement message acknowledgement using the `AckSession` pattern, allowing senders to track multiple receipts of a message.

## Phase 1: Models & Serialization
Focus on updating the data structures to support the new metadata and the specialized Ack message payload.

- [x] Task: Create `AckMessageContent` class in `MulticastTransportFramework`.
    - [x] Define a class implementing `ITransportMessageContent`.
    - [x] Add `Guid OriginalMessageId` property.
- [x] Task: Update `TransportMessage` class.
    - [x] Add `bool RequestAck { get; set; }` with `DefaultValueHandling.Ignore`.
- [x] Task: Create `AckSession` class.
    - [x] Implement `OriginalMessageId`, `IsAnyAckReceived`, and `ReceivedAcks` (using `ConcurrentBag`).
    - [x] Implement `event Action<AckSession, EventSource> OnAckReceived`.
    - [x] Implement `WaitAsync(TimeSpan timeout)` logic.
- [x] Task: Conductor - User Manual Verification 'Phase 1: Models & Serialization' (Protocol in workflow.md)

## Phase 2: TransportComponent Infrastructure
Integrate the session management into the central transport hub.

- [x] Task: Add state management to `TransportComponent`.
    - [x] Add `public TimeSpan DefaultAckTimeout { get; set; } = TimeSpan.FromSeconds(5);`.
    - [x] Add `ConcurrentDictionary<Guid, AckSession> _activeSessions`.
- [x] Task: Refactor `TransportComponent.Send`.
    - [x] Change return type from `string` to `AckSession`.
    - [x] Logic: If `message.RequestAck`, create and register `AckSession`.
    - [x] Trigger the timeout timer for the session.
- [x] Task: Implement `TransportComponent.SendAck`.
    - [x] Create a `TransportMessage` with `MessageType = 99`.
    - [x] Ensure the Ack message itself has `RequestAck = false`.
- [x] Task: Conductor - User Manual Verification 'Phase 2: TransportComponent Infrastructure' (Protocol in workflow.md)

## Phase 3: Ack Processing & Cleanup
Implement the logic to receive and route Acks to their respective sessions.

- [x] Task: Update `socket_OnNotifyMulticastSocketListener` or internal routing.
    - [x] Intercept messages with `MessageType = 99`.
    - [x] Resolve `AckMessageContent`.
    - [x] Match `OriginalMessageId` with `_activeSessions`.
    - [x] Call `session.ReportAck(source)`.
- [x] Task: Implement Cleanup.
    - [x] Ensure sessions are removed from `_activeSessions` after `WaitAsync` completes or timeout fires.
    - [x] Ensure sessions are removed from `_activeSessions` after `WaitAsync` completes or timeout fires.
- [x] Task: Conductor - User Manual Verification 'Phase 3: Ack Processing & Cleanup' (Protocol in workflow.md)

## Phase 4: Verification & Samples
Ensure quality and demonstrate the feature to users.

- [x] Task: Fix breaking changes in Tests.
    - [x] Update `GateKeeperDeadlockTests` and others where `Send` return value was used.
- [x] Task: Add TDD unit tests for Ack.
    - [x] Test successful single ack.
    - [x] Test multiple acks from different sources.
    - [x] Test timeout (no acks).
    - [x] Test that `RequestAck: false` is not serialized.
- [x] Task: Update `SampleApp`.
    - [x] Add a command or logic to send a message with Ack request.
    - [x] Print out acks as they arrive via `OnAckReceived`.
- [x] Task: Conductor - User Manual Verification 'Phase 4: Verification & Samples' (Protocol in workflow.md)
