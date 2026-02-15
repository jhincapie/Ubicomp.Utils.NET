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
        private byte[] _integrityKey;
        private byte[] _serializedWithIntegrity;

        [GlobalSetup]
        public void Setup()
        {
            _integrityKey = new byte[32];
            new Random().NextBytes(_integrityKey);

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

            // Pre-serialize for deserialization benchmark
            BinaryPacket.SerializeToWriter(_writer, _message, _integrityKey, (Ubicomp.Utils.NET.MulticastTransportFramework.EncryptorDelegate?)null, _jsonOptions);
            _serializedWithIntegrity = _writer.WrittenSpan.ToArray();
        }

        [Benchmark]
        public void SerializeToWriter()
        {
            _writer.Clear();
            _message.SenderSequenceNumber = 1;
            BinaryPacket.SerializeToWriter(_writer, _message, (byte[]?)null, (Ubicomp.Utils.NET.MulticastTransportFramework.EncryptorDelegate?)null, _jsonOptions);
        }

        [Benchmark]
        public void SerializeWithIntegrity()
        {
            _writer.Clear();
            _message.SenderSequenceNumber = 1;
            BinaryPacket.SerializeToWriter(_writer, _message, _integrityKey, (Ubicomp.Utils.NET.MulticastTransportFramework.EncryptorDelegate?)null, _jsonOptions);
        }

        [Benchmark]
        public void DeserializeWithIntegrity()
        {
            BinaryPacket.Deserialize(_serializedWithIntegrity, _jsonOptions, (Ubicomp.Utils.NET.MulticastTransportFramework.DecryptorDelegate?)null, _integrityKey);
        }
    }
}
