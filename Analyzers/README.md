# Code Analyzers

The **Analyzers** project contains Roslyn Analyzers to enforce best practices and correct usage of the `MulticastTransportFramework`.

## Rules

### UBI001: Missing MessageType Attribute
*   **Severity**: Error
*   **Description**: Ensures that any type passed to `TransportComponent.SendAsync<T>` is decorated with the `[MessageType]` attribute.
*   **Why**: The transport layer requires a unique string ID for routing. Without the attribute, the message cannot be routed correctly unless manually registered (which is error-prone).

**Incorrect Code:**
```csharp
public class MyData { } // Missing Attribute
await transport.SendAsync(new MyData()); // Error UBI001
```

**Correct Code:**
```csharp
[MessageType("app.mydata")]
public class MyData { }
await transport.SendAsync(new MyData()); // OK
```

## Dependencies
*   `Microsoft.CodeAnalysis.CSharp`
