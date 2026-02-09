using System;
using System.Collections.Generic;
using System.Buffers;
using BenchmarkDotNet.Attributes;
using System.Text.Json;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class BinaryPacketBenchmark
    {
        private TransportMessage _message;
        private JsonSerializerOptions _jsonOptions;
        private System.Buffers.ArrayBufferWriter<byte> _writer;

        [GlobalSetup]
        public void Setup()
        {
            var payload = new BenchmarkPayload
            {
                Id = 123,
                Name = "Test Payload",
                Description = "This is a benchmark payload with some data to serialize.",
                Values = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                Timestamp = DateTime.UtcNow
            };

            var source = new EventSource(Guid.NewGuid(), "BenchmarkSource", "Friendly Source");

            _message = new TransportMessage(source, "benchmark.payload", payload)
            {
                RequestAck = true,
                IsEncrypted = false // Ensure we hit the unencrypted path
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            _writer = new System.Buffers.ArrayBufferWriter<byte>();
        }

        [Benchmark]
        public void SerializeToWriter()
        {
            _writer.Clear();
            BinaryPacket.SerializeToWriter(_writer, _message, 1, (byte[]?)null, (Ubicomp.Utils.NET.MulticastTransportFramework.EncryptorDelegate?)null, _jsonOptions);
        }
    }
}
