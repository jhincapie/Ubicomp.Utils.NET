# Ubicomp.Utils.NET Solution Context

## Core Mandates
*   **NEVER push directly to the `master` branch.** All changes must be submitted via Pull Requests for review.
*   **Target Framework**: All projects target **.NET 8.0**.

## Project Overview
**Ubicomp.Utils.NET** is a collection of libraries designed to facilitate the development of context-aware and networked applications. It provides frameworks for multicast communication and context monitoring.

## Architecture & Components

The solution is structured into several key projects:

### 1. ContextAwarenessFramework
*   **Pattern**: Monitor-Service-Entity (MSE).
*   **Role**: Provides infrastructure for context monitoring and data transformation.
*   **Key Concept**: Separates data acquisition (`ContextMonitor`) from logic (`ContextService`) and state (`IEntity`).

### 2. MulticastTransportFramework
*   **Role**: Higher-level reliable messaging layer over UDP multicast.
*   **Architecture**: Actor-like model with dedicated internal loops (`ProcessingLoop`).
*   **Key Features**:
    - **Reliability**: `ReplayProtector` (deduplication), `AckSession` (delivery confirmation).
    - **Security**: AES-GCM encryption and HMAC-SHA256 integrity.
    - **Serialization**: Dual support for `BinaryPacket` (optimized) and JSON.
    - **Discovery**: Uses `Generators` for auto-wiring message types.

### 3. MulticastSocket
*   **Role**: Low-level wrapper for .NET UDP Sockets.
*   **Architecture**: Uses `System.Threading.Channels` to decouple receive logic from consumption.
*   **Key Features**: `IAsyncEnumerable` streaming, object/array pooling for performance.

### 4. Tooling
*   **Generators**: Scans code for `[MessageType]` attributes and generates registration code (`TransportExtensions`).
*   **Analyzers**: Enforces usage of `[MessageType]` via `UBI001` (Error).
*   **CLI**: Provides network diagnostics (`check`) and packet sniffing (`sniff`) capabilities.

### 5. Applications & Tests
*   **`SampleApp`**: Demonstrates library usage.
*   **`Tests`**: Comprehensive unit tests covering all layers.
*   **`Benchmarks`**: Performance comparisons (JSON vs Binary).
*   **`Multicast.TestApp`**: Isolated transport testing.
*   **`ContextAwarenessFramework.TestApp`**: Isolated CAF testing.

## Development Workflow

### Build & Run Commands
All commands should be run from the repository root.

*   **Build Solution:**
    ```bash
    dotnet build
    ```

*   **Run Sample App:**
    ```bash
    dotnet run --project SampleApp/Ubicomp.Utils.NET.SampleApp.csproj -- --no-wait
    ```

*   **Run Tests:**
    ```bash
    dotnet test Tests/Ubicomp.Utils.NET.Tests.csproj
    ```

## Development Conventions
*   **Async/Await**: Prefer asynchronous patterns. The networking layer uses `IAsyncEnumerable` and `Channels`.
*   **Messaging**: Use `[MessageType("id")]` attributes on POCOs for transport routing.
*   **Dependencies**: Prefer NuGet packages. Ensure all libraries target `net8.0`.

## Important Files
*   `Ubicomp.Utils.NET.sln`: The main solution file.
*   `README.md`: Basic instructions and authorship.
