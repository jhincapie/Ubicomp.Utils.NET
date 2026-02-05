# Generators Context

## Overview
**Ubicomp.Utils.NET.Generators** is a Roslyn Source Generator project (targeting `netstandard2.0`) that implements `IIncrementalGenerator`.

## Functionality
*   **Target**: Classes decorated with `[MessageType("id")]`.
*   **Output**: `TransportExtensions.g.cs`.
*   **Namespace**: `Ubicomp.Utils.NET.Generators.AutoDiscovery`.
*   **Method**: `RegisterDiscoveredMessages(this TransportComponent component)`.

## Integration
*   The `TransportBuilder.Build()` method in `MulticastTransportFramework` uses reflection to find `Ubicomp.Utils.NET.Generators.AutoDiscovery.TransportExtensions` and invoke `RegisterDiscoveredMessages`.
*   This allows consumers to simply define message types, and the framework automatically becomes aware of them without manual registration calls.

## Dependencies
*   `Microsoft.CodeAnalysis.CSharp`
*   `Microsoft.CodeAnalysis.Analyzers`
