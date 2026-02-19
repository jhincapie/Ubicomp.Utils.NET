# Benchmarks

The **Benchmarks** project uses [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) to measure the performance of critical components in the `MulticastTransportFramework`.

## Scenarios

### 1. Serialization
Compares the performance of serializing a `TransportMessage` payload.
*   **Newtonsoft.Json**: Legacy baseline.
*   **System.Text.Json**: Modern, high-performance JSON (Target).
*   **BinaryPacket**: Custom binary protocol.

**Goal**: Validate that `System.Text.Json` provides ~2x improvement over Newtonsoft, and `BinaryPacket` provides further gains in size and speed.

### 2. Binary Packet Construction
Measures the overhead of writing the `BinaryPacket` structure (Header + Payload) to an `ArrayBufferWriter<byte>`.

### 3. Binary Packet Integrity (HMAC-SHA256)
Benchmarks the cost of adding cryptographic integrity checks to the `BinaryPacket`.
*   **SerializeWithIntegrity**: Computes HMAC-SHA256 during serialization (zero-allocation via `stackalloc`).
*   **DeserializeWithIntegrity**: Verifies HMAC-SHA256 during deserialization.

**Goal**: Ensure that integrity checks introduce minimal overhead compared to raw serialization.

## Running Benchmarks
Run the project in **Release** mode:

```bash
dotnet run -c Release --project Benchmarks/Benchmarks.csproj
```
