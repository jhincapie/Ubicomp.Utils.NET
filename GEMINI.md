# Ubicomp.Utils.NET Project Context

## Core Mandates
*   **NEVER push directly to the `master` branch.** All changes must be submitted via Pull Requests for review.

## Project Overview
**Ubicomp.Utils.NET** is a collection of .NET libraries designed to facilitate the development of context-aware and networked applications. It provides frameworks for multicast communication and context monitoring, abstracting complex networking and data management tasks.

The solution consists of core libraries targeting `netstandard2.0` for broad compatibility, along with sample applications and tests targeting modern .NET versions (e.g., .NET 8.0).

## Architecture & Components

The solution is structured into several key projects:

### Core Libraries (Target: `netstandard2.0`)
*   **`ContextAwarenessFramework`**: Provides the infrastructure for context monitoring, service listening, and data transformation.
*   **`MulticastSocket`**: A utility wrapper around multicast networking capabilities.
*   **`MulticastTransportFramework`**: Builds upon `MulticastSocket` to provide a higher-level message transport mechanism.

### Applications & Tests (Target: `net8.0`)
*   **`SampleApp`**: Demonstrates the usage of the libraries.
*   **`ContextAwarenessFramework.TestApp`**: Specific test application for the context framework.
*   **`Multicast.TestApp`**: Specific test application for multicast functionality.
*   **`Tests`**: Unit tests for the solution (likely using MSTest, NUnit, or xUnit - check `Ubicomp.Utils.NET.Tests.csproj`).

### External Dependencies
*   All dependencies are managed via NuGet. The project is aligned with **.NET 8.0** standards (using version `8.0.0` for Microsoft extensions).
*   Key libraries: `Microsoft.Extensions.Logging`, `System.Threading.Channels`, `Microsoft.Bcl.AsyncInterfaces`, `Newtonsoft.Json`.

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
    dotnet run --project SampleApp/Ubicomp.Utils.NET.SampleApp.csproj
    ```
    *Note: Use `--no-wait` argument for automated testing to avoid blocking on `Console.ReadKey()`.*
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
