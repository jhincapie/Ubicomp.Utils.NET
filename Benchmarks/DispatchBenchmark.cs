using System;
using System.Collections.Concurrent;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    public class DispatchBenchmark
    {
        private ConcurrentDictionary<string, Delegate> _genericHandlers = null!;
        private ConcurrentDictionary<string, Action<object, object>> _optimizedHandlers = null!;
        private string _messageType = "test.message";
        private object _messageData = "test data";
        private object _context = new object();

        [GlobalSetup]
        public void Setup()
        {
            _genericHandlers = new ConcurrentDictionary<string, Delegate>();
            _optimizedHandlers = new ConcurrentDictionary<string, Action<object, object>>();

            // Simulate the original registration
            Action<string, object> originalHandler = (data, ctx) => { };
            _genericHandlers[_messageType] = originalHandler;

            // Simulate the optimized registration
            Action<string, object> handler = (data, ctx) => { };
            Action<object, object> optimizedWrapper = (data, ctx) => handler((string)data, ctx);
            _optimizedHandlers[_messageType] = optimizedWrapper;
        }

        [Benchmark(Baseline = true)]
        public void DynamicInvoke()
        {
            if (_genericHandlers.TryGetValue(_messageType, out var handler))
            {
                handler.DynamicInvoke(_messageData, _context);
            }
        }

        [Benchmark]
        public void OptimizedInvoke()
        {
            if (_optimizedHandlers.TryGetValue(_messageType, out var handler))
            {
                handler(_messageData, _context);
            }
        }
    }
}
