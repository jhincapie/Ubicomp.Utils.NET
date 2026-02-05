# ContextAwarenessFramework.TestApp

A testbed application for the **ContextAwarenessFramework**.

## Purpose
Validates the Monitor-Service-Entity (MSE) pattern implementation.
*   **Monitors**: Simulates context sensors (e.g., Simulated Location, Random Value Generator).
*   **Services**: Tests the aggregation and logic layer.
*   **Entities**: Verifies `INotifyPropertyChanged` behavior and data binding flow.

## Usage
```bash
dotnet run --project ContextAwarenessFramework.TestApp/ContextAwarenessFramework.TestApp.csproj
```
