# ContextAwarenessFramework.TestApp

A testbed application for the **ContextAwarenessFramework**.

## Purpose
Validates the Monitor-Service-Entity (MSE) pattern implementation.
*   **Monitors**: Uses `TestMonitor` to simulate context sensing and generate random test data.
*   **Services**: Uses `TestService` to demonstrate data aggregation and event handling.
*   **Entities**: Uses `TestEntity` to verify `INotifyPropertyChanged` behavior and data binding flow.

## Usage
```bash
dotnet run --project ContextAwarenessFramework.TestApp/ContextAwarenessFramework.TestApp.csproj
```
