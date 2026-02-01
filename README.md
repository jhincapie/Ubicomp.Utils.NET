# Ubicomp.Utils.NET

A set of libraries to ease the creation of context aware and networked applications on the .NET environment.

Created By: Juan David Hincapie-Ramos - http://blog.jhincapie.com

## How to Run

### Linux (using .NET CLI)

1.  **Install .NET SDK**: Ensure you have the .NET SDK installed (version 8.0 or 10.0 recommended).
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

---
*Note: The core libraries target `netstandard2.0` for maximum compatibility, while the Sample App and Tests target `net10.0` (or `net8.0`).*
