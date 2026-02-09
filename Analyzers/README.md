# Ubicomp.Utils.NET.Analyzers

This project contains standalone Roslyn Analyzers to enforce coding standards and correct usage of the Ubicomp frameworks.

## Analyzers

### 1. Message Type Analyzer (UBI001)
*   **ID**: `UBI001`
*   **Severity**: **Error**
*   **Description**: Enforces that any type passed to `TransportComponent.SendAsync<T>` is decorated with the `[MessageType]` attribute.
*   **Behavior**: Unlike the warning in the `Generators` project (`UbicompNET001`), this analyzer treats the missing attribute as a build error to prevent runtime routing failures.

## Usage
Add a reference to this project (or NuGet package) in your consumer project to enable real-time analysis in the IDE and during build.
