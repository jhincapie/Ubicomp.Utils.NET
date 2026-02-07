# Ubicomp.Utils.NET.Analyzers

This project contains Roslyn Analyzers to enforce coding standards and best practices within the Ubicomp.Utils.NET solution.

## Analyzers

### UBI001: Type argument must have [MessageType] attribute
*   **Severity**: Error
*   **Description**: Ensures that any type used as a generic argument in `TransportComponent.SendAsync<T>` is decorated with the `[MessageType]` attribute. This is crucial for the auto-discovery mechanism to work correctly.
*   **Category**: Usage

## Usage
This analyzer is automatically enabled when the project is referenced. No additional configuration is required.
