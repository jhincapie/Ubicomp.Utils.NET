# ContextAwarenessFramework.TestApp Context

## Purpose
Testbed for the Monitor-Service-Entity (MSE) pattern.

## Architecture
*   **Monitors**: `TestMonitor` (generates random string data).
*   **Services**: `TestService` (aggregates/logs incoming data).
*   **Entities**: `TestEntity` (POCO implementing `IEntity` and `INotifyPropertyChanged`).

## Goal
To verify that the decoupling between Monitors (Producers) and Services (Consumers) works correctly, including threading behavior and event propagation.

## Do's and Don'ts
*   **Do** use `TestMonitor` to generate predictable test data.
*   **Do** verify that services correctly aggregate data from multiple monitors.
*   **Don't** rely on real hardware sensors in this test app.
