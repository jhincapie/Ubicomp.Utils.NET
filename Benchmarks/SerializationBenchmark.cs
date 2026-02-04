using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Newtonsoft.Json;
using System.Text.Json;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class SerializationBenchmark
    {
        private TransportMessage _message;
        private string _jsonNewtonsoft;
        private string _jsonSystemText;
        private JsonSerializerSettings _newtonsoftSettings;
        private JsonSerializerOptions _systemTextOptions;
        private Dictionary<string, Type> _knownTypes;

        [GlobalSetup]
        public void Setup()
        {
            _knownTypes = new Dictionary<string, Type>
            {
                { "benchmark.payload", typeof(BenchmarkPayload) }
            };

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
                RequestAck = true
            };

            // Newtonsoft Setup
            _newtonsoftSettings = new JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
            };
            _newtonsoftSettings.Converters.Add(new NewtonsoftTransportMessageConverter(_knownTypes));

            // System.Text.Json Setup
            _systemTextOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            // Pre-serialize for deserialization benchmarks
            _jsonNewtonsoft = JsonConvert.SerializeObject(_message, _newtonsoftSettings);
            _jsonSystemText = System.Text.Json.JsonSerializer.Serialize(_message, _systemTextOptions);
        }

        [Benchmark(Baseline = true)]
        public string NewtonsoftSerialize()
        {
            return JsonConvert.SerializeObject(_message, _newtonsoftSettings);
        }

        [Benchmark]
        public string SystemTextJsonSerialize()
        {
            return System.Text.Json.JsonSerializer.Serialize(_message, _systemTextOptions);
        }

        [Benchmark]
        public TransportMessage NewtonsoftDeserialize()
        {
            return JsonConvert.DeserializeObject<TransportMessage>(_jsonNewtonsoft, _newtonsoftSettings)!;
        }

        [Benchmark]
        public TransportMessage SystemTextJsonDeserialize()
        {
            var msg = System.Text.Json.JsonSerializer.Deserialize<TransportMessage>(_jsonSystemText, _systemTextOptions)!;
            if (msg.MessageData is JsonElement element)
            {
                msg.MessageData = System.Text.Json.JsonSerializer.Deserialize(element.GetRawText(), _knownTypes[msg.MessageType], _systemTextOptions)!;
            }
            return msg;
        }
    }

    public class BenchmarkPayload
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<int> Values { get; set; } = new List<int>();
        public DateTime Timestamp { get; set; }
    }
}
