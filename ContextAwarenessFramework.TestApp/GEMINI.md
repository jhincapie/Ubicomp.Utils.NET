# ContextAwarenessFramework.TestApp Context

## Purpose
Testbed for the Monitor-Service-Entity (MSE) pattern.

## Architecture
*   **Monitors**: Contains simulated monitors (e.g., `SimulatedLocationMonitor`) that produce dummy data.
*   **Services**: Contains test services that aggregate this data.
*   **Entities**: Contains POCOs implementing `IEntity`.

## Goal
To verify that the decoupling between Monitors (Producers) and Services (Consumers) works correctly, including threading behavior and event propagation.

## Do's and Don'ts
*   **Do** use simulated monitors to generate predictable test data.
*   **Do** verify that services correctly aggregate data from multiple monitors.
*   **Don't** rely on real hardware sensors in this test app.
