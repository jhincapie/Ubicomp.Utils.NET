# Generators Context

## Overview
**Ubicomp.Utils.NET.Generators** is a Roslyn project (targeting `net8.0`) that implements:
1.  `IIncrementalGenerator`: For source generation.
2.  `DiagnosticAnalyzer`: For code analysis.

## Components

### Source Generator (`MessageTypeGenerator`)
*   **Target**: Classes decorated with `[MessageType("id")]`.
*   **Output**: `TransportExtensions.g.cs`.
*   **Namespace**: `Ubicomp.Utils.NET.Generators.AutoDiscovery`.
*   **Method**: `RegisterDiscoveredMessages(this TransportComponent component)`.

### Analyzer (`MessageTypeAnalyzer`)
*   **ID**: `UbicompNET001`
*   **Severity**: Warning
*   **Target**: Invocations of `TransportComponent.SendAsync<T>`.
*   **Logic**: Verifies that `T` has the `[MessageType]` attribute.

## Integration
*   The `TransportBuilder.Build()` method in `MulticastTransportFramework` uses reflection to find `Ubicomp.Utils.NET.Generators.AutoDiscovery.TransportExtensions` and invoke `RegisterDiscoveredMessages`.

## Dependencies
*   `Microsoft.CodeAnalysis.CSharp`
*   `Microsoft.CodeAnalysis.Analyzers`

## Do's and Don'ts
*   **Do** decorate message classes with `[MessageType("id")]` to enable auto-discovery.
*   **Do** ensure the `Generators` project is referenced as an analyzer in the consuming project.
*   **Don't** manually call `RegisterMessageType` if you are using `TransportBuilder.Build()`, as it handles this automatically.
