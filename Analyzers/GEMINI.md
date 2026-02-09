# Analyzers Context

## Overview
**Ubicomp.Utils.NET.Analyzers** is a Roslyn project (targeting `netstandard2.0`) containing diagnostic analyzers.

## Components

### Analyzer (`MessageTypeAnalyzer`)
*   **ID**: `UBI001`
*   **Severity**: **Error**
*   **Target**: Invocations of `TransportComponent.SendAsync<T>`.
*   **Logic**: Verifies that `T` has the `[MessageType]` attribute.
*   **Distinction**: This is a stricter version (Error) of the analyzer found in the `Generators` project (Warning: `UbicompNET001`).

## Dependencies
*   `Microsoft.CodeAnalysis.CSharp`
*   `Microsoft.CodeAnalysis.Analyzers`

## Do's and Don'ts
*   **Do** fix `UBI001` errors by adding `[MessageType("id")]` to your message classes.
*   **Don't** ignore this error; it indicates a high probability of runtime message loss.
