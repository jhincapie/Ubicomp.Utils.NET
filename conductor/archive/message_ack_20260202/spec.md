# Specification: Message Acknowledgement (Ack) Support

## Overview
Introduce a robust mechanism for message acknowledgement in the `MulticastTransportFramework`. This allows senders to track receipt of their messages by one or more receivers via a new `AckSession` object.

## Functional Requirements

### 1. TransportMessage Enhancements
- Add a `RequestAck` property (boolean) to the `TransportMessage` class.
- **Default Value**: Must default to `false`.
- **Efficiency**: Use `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]` so the property is only serialized when `true`.

### 2. AckSession Class
- **Purpose**: Represents an active tracking session for acknowledgements of a specific message.
- **Properties**:
    - `OriginalMessageId`: The GUID of the message being tracked.
    - `IsAnyAckReceived`: Boolean, true if at least one Ack has arrived.
    - `ReceivedAcks`: A thread-safe collection (e.g., `ConcurrentBag<EventSource>`) representing the responders.
- **Events**:
    - `event Action<AckSession, EventSource> OnAckReceived`: Fired immediately whenever a valid Ack for this message arrives.
- **Methods**:
    - `Task<bool> WaitAsync()`: Awaitable method that completes after the timeout. Returns `true` if at least one Ack was received, otherwise `false`.

### 3. TransportComponent - Sending and Tracking
- **Refactored Method**: `public AckSession Send(TransportMessage message)`
    - Returns an `AckSession`.
    - If `RequestAck` is `true`, the session is registered in a `ConcurrentDictionary<Guid, AckSession>` and starts its timeout timer.
    - If `RequestAck` is `false`, the session returns a completed `WaitAsync()` task immediately.
- **Timeout Management**:
    - Use `TransportComponent.DefaultAckTimeout` (default 5s).
- **Cleanup**: Ensure `AckSession` is removed from the internal dictionary after completion (timeout or otherwise).

### 4. TransportComponent - Acknowledging Messages
- **New Method**: `public void SendAck(TransportMessage originalMessage)`
    - Receivers call this to acknowledge a message.
    - Sends a `TransportMessage` with `MessageType = 99` and an `AckMessageContent` payload.
    - **CRITICAL**: The Ack message itself MUST have `RequestAck = false`.

### 5. TransportComponent - Processing Acks
- Internal logic to catch `MessageType = 99`.
- Look up the `AckSession` by GUID in the registry.
- If found, trigger the session's `OnAckReceived` event and update its state.

## Acceptance Criteria
- [ ] `Send(msg)` returns an `AckSession`.
- [ ] Subscribing to `AckSession.OnAckReceived` works for multiple receivers.
- [ ] `await session.WaitAsync()` returns `true` if 1+ acks arrive, `false` after timeout.
- [ ] The JSON payload of an Ack message does not include the `RequestAck` key.
- [ ] The JSON payload of a standard message with `RequestAck = false` does not include the `RequestAck` key.

## Out of Scope
- **Retry Logic**: No automatic re-sending of the original message.
