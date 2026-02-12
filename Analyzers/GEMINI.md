# Analyzers Context

## Module Purpose
**Analyzers** provides static analysis rules for the solution.

## Key Components
*   **`MessageTypeAnalyzer`**:
    *   **ID**: `UBI001`
    *   **Severity**: Error
    *   **Logic**: Inspects invocations of `SendAsync<T>`. Checks if `T` has `[MessageType]`.

## Do's and Don'ts
*   **Do** keep analyzers lightweight to avoid slowing down the IDE.
*   **Do** provide clear diagnostic messages and code fixes where possible.
