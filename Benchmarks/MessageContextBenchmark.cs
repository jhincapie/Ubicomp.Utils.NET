using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    public class MessageContextBenchmark
    {
        private TransportMessage _message;
        private EventSource _source;

        [GlobalSetup]
        public void Setup()
        {
            _source = new EventSource(Guid.NewGuid(), "BenchmarkSource");
            _message = new TransportMessage(_source, "test.message", new object());
        }

        [Benchmark(Baseline = true)]
        public MessageContext CreateContext_Legacy()
        {
            // Simulate current overhead: Accessing TimeStamp property allocates string
            string ts = _message.TimeStamp;
            #pragma warning disable CS0618 // Type or member is obsolete
            return new MessageContext(_message.MessageId, _message.MessageSource, ts, _message.RequestAck);
            #pragma warning restore CS0618 // Type or member is obsolete
        }

        [Benchmark]
        public MessageContext CreateContext_Optimized()
        {
            // Optimized path: Passing ticks directly
            return new MessageContext(_message.MessageId, _message.MessageSource, _message.Ticks, _message.RequestAck);
        }
    }
}
