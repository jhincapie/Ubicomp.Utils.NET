# Ubicomp.Utils.NET

A collection of .NET libraries designed to facilitate the development of context-aware and networked applications. It abstracts complex networking, messaging, and data management tasks into a layered, reusable architecture.

Created By: Juan David Hincapie-Ramos - http://blog.jhincapie.com

## Architecture
The project follows a layered architecture, ranging from low-level network transport to high-level context management.

![System Architecture Diagram](assets/system_architecture_diagram.png)

### 1. ContextAwarenessFramework (CAF)
The top layer implements the **Monitor-Service-Entity (MSE)** pattern.
- **ContextMonitors** collect raw data from sensors or external APIs.
- **ContextServices** aggregate monitor data, implement business logic, and handle persistence.
- **IEntities** represent the context data models, providing thread-safe change notifications.

### 2. MulticastTransportFramework
The middle layer provides a structured messaging protocol over UDP multicast.
- **TransportComponent**: The central orchestrator (Singleton).
- **Serialization**: Automated JSON serialization/deserialization with polymorphic support.
- **Ordering (GateKeeper)**: Ensures messages are processed in the exact order they were received, even in asynchronous environments.
- **Reliability (Ack)**: Optional acknowledgement-based sessions for reliable message delivery.
- **Diagnostics**: Built-in tools for network sanity and firewall checks.

### 3. MulticastSocket
The foundational layer that wraps standard .NET UDP sockets.
- **Group Management**: Simplified joining and leaving of multicast groups.
- **Sequencing**: Automatically assigns sequence numbers to incoming packets for higher-level ordering.
- **Performance**: Optimized async I/O and buffer management.

## Project Documentation
*   [**MulticastSocket**](MulticastSocket/README.md): Low-level multicast networking wrapper.
*   [**MulticastTransportFramework**](MulticastTransportFramework/README.md): High-level messaging and transport layer.
*   [**ContextAwarenessFramework**](ContextAwarenessFramework/README.md): Framework for context sensing and data management.

## Core Flow
1.  **Network Receive**: `MulticastSocket` receives bytes and assigns a sequence ID.
2.  **Transport Processing**: `TransportComponent` deserializes the JSON into a typed `TransportMessage`.
3.  **Ordered Dispatch**: The `GateKeeper` holds the message until its sequence ID is next, then dispatches it to registered handlers.
4.  **Context Update**: A `ContextService` (acting as a listener) receives the message and updates its `IEntity` state.
5.  **UI Notification**: The `ContextService` uses a captured `Dispatcher` to safely notify UI components of the change.

## Modernization Status
This project targets **.NET Standard 2.0** for core libraries (for broad compatibility) and **.NET 8.0** for applications and tests.
- **Serialization**: `Newtonsoft.Json`.
- **Logging**: `Microsoft.Extensions.Logging`.
- **Dependencies**: Managed via NuGet.

## How to Run

### Linux (using .NET CLI)

1.  **Install .NET SDK**: Ensure you have the .NET SDK installed (version 8.0 recommended).
2.  **Clone the repository**:
    ```bash
    git clone https://github.com/jhincapie/Ubicomp.Utils.NET.git
    cd Ubicomp.Utils.NET
    ```
3.  **Build the solution**:
    ```bash
    dotnet build
    ```
4.  **Run the Sample App**:
    ```bash
    dotnet run --project SampleApp/Ubicomp.Utils.NET.SampleApp.csproj
    ```
5.  **Run Tests**:
    ```bash
    dotnet test Tests/Ubicomp.Utils.NET.Tests.csproj
    ```
6.  **Format Code**:
    ```bash
    dotnet format
    ```

### Windows

#### Using Visual Studio
1.  Open `Ubicomp.Utils.NET.sln` in Visual Studio 2022 or later.
2.  The projects have been updated to the modern SDK-style format.
3.  Right-click on `Ubicomp.Utils.NET.SampleApp` and select **Set as Startup Project**.
4.  Press **F5** to run.
5.  Open **Test Explorer** to run the unit tests.

#### Using .NET CLI (PowerShell/CMD)
1.  **Build**: `dotnet build`
2.  **Run Sample**: `dotnet run --project SampleApp\Ubicomp.Utils.NET.SampleApp.csproj`
3.  **Run Tests**: `dotnet test Tests\Ubicomp.Utils.NET.Tests.csproj`
4.  **Format Code**: `dotnet format`

## Contribution Guidelines
*   **Do NOT push directly to the `master` branch.** 
*   Always create a feature branch and submit a Pull Request.

---
*Note: The core libraries target `netstandard2.0` for maximum compatibility, while the Sample App and Tests target `net8.0`.*