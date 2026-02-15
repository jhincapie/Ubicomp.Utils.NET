# Generators Context

## Module Purpose
**Generators** automates the registration of message types for `MulticastTransportFramework`.

## Key Components
*   **`MessageTypeGenerator`**: Implements `IIncrementalGenerator`.
    *   **Predicate**: Finds classes with `[MessageType]`.
    *   **Transform**: Extracts the ID string from `[MessageType]` attribute.
    *   **Output**: Generates `TransportExtensions.g.cs` in `Ubicomp.Utils.NET.Generators.AutoDiscovery` namespace.

## Usage
The generated `RegisterDiscoveredMessages` method is automatically located and invoked by `TransportBuilder` via reflection. This populates the internal type map (`_knownTypes`), allowing the `TransportComponent` to deserialize packet payloads into the correct CLR types before dispatching them.

## Do's and Don'ts
*   **Do** ensure the generated code is efficient and valid C#.
*   **Don't** depend on runtime reflection inside the generator; use the Roslyn Semantic Model.

## File Structure
*   `MessageTypeGenerator.cs`: The source generator logic.
*   `MessageTypeAnalyzer.cs`: (Legacy) Analyzer logic (see `Analyzers` project for primary analyzer).
