# Ubicomp.Utils.NET Project Context

## Core Mandates
*   **NEVER push directly to the `master` branch.** All changes must be submitted via Pull Requests for review.

## Project Overview
**Ubicomp.Utils.NET** is a collection of .NET libraries designed to facilitate the development of context-aware and networked applications. It provides frameworks for multicast communication and context monitoring, abstracting complex networking and data management tasks.

The solution consists of core libraries targeting `netstandard2.0` for broad compatibility, along with sample applications and tests targeting modern .NET versions (e.g., .NET 8.0).

## Architecture & Components

The solution is structured into several key projects:

### 1. ContextAwarenessFramework (`netstandard2.0`)
*   **Pattern**: Monitor-Service-Entity (MSE).
*   **Role**: Provides infrastructure for context monitoring and data transformation.
*   **Key Concept**: Separates data acquisition (`ContextMonitor`) from logic (`ContextService`) and state (`IEntity`).

### 2. MulticastTransportFramework (`netstandard2.0`)
*   **Role**: Higher-level reliable messaging layer over UDP multicast.
*   **Architecture**: Actor-like model with dedicated internal loops (`GateKeeperLoop`, `ProcessingLoop`).
*   **Key Features**:
    - **Reliability**: `GateKeeper` (ordering via PriorityQueue), `ReplayWindow` (deduplication), `AckSession` (delivery confirmation).
    - **Security**: AES-GCM encryption (Modern) / AES-CBC (Legacy) and HMAC-SHA256 integrity.
    - **Serialization**: Dual support for `BinaryPacket` (optimized) and JSON (via `System.Text.Json`).
    - **Discovery**: Uses `Generators` for auto-wiring message types.

### 3. MulticastSocket (`netstandard2.0`)
*   **Role**: Low-level wrapper for .NET UDP Sockets.
*   **Architecture**: Uses `System.Threading.Channels` to decouple receive logic from consumption.
*   **Key Features**: `IAsyncEnumerable` streaming, object/array pooling for performance.
*   **Compatibility**: Wraps legacy APM (`BeginReceiveFrom`) for `netstandard2.0` and `ReceiveFromAsync` for modern runtimes.

### 4. Generators (`netstandard2.0`)
*   **Role**: Roslyn Source Generator.
*   **Function**: Scans code for `[MessageType]` attributes and generates extension methods to automatically register them with the Transport component.

### Applications & Tests (`net8.0`)
*   **`SampleApp`**: Demonstrates library usage.
*   **`ContextAwarenessFramework.TestApp`**: Context framework testbed.
*   **`Multicast.TestApp`**: Multicast functionality testbed.
*   **`Tests`**: Comprehensive unit tests covering all layers.

## Development Workflow

### Prerequisites
*   .NET SDK (Version 8.0 recommended).

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

*   **Format Code:**
    ```bash
    dotnet format
    ```

## Development Conventions
*   **Async/Await**: Prefer asynchronous patterns. The networking layer uses `IAsyncEnumerable` and `Channels`.
*   **Messaging**: Use `[MessageType("id")]` attributes on POCOs for transport routing.
*   **Project Format**: Modern SDK-style `.csproj` files are used.
*   **Dependencies**: Prefer NuGet packages. Maintain `netstandard2.0` compatibility for core libraries.

## Important Files
*   `Ubicomp.Utils.NET.sln`: The main solution file.
*   `README.md`: Basic instructions and authorship.
