# Generators Context

## Module Purpose
**Generators** automates the registration of message types for `MulticastTransportFramework`.

## Key Components
*   **`MessageTypeGenerator`**: Implements `IIncrementalGenerator`.
    *   **Predicate**: Finds classes with `[MessageType]`.
    *   **Transform**: Extracts the ID string from `[MessageType]` attribute.
    *   **Output**: Generates `TransportExtensions.g.cs` in `Ubicomp.Utils.NET.Generators.AutoDiscovery` namespace.

## Usage
Referenced as an `Analyzer` by the `MulticastTransportFramework` or consumer projects.

## Do's and Don'ts
*   **Do** ensure the generated code is efficient and valid C#.
*   **Don't** depend on runtime reflection inside the generator; use the Roslyn Semantic Model.

## File Structure
*   `MessageTypeGenerator.cs`: The source generator logic.
*   `MessageTypeAnalyzer.cs`: (Legacy) Analyzer logic (see `Analyzers` project for primary analyzer).
