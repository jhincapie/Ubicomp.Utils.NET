using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;
using Xunit.Abstractions;

namespace Ubicomp.Utils.NET.Tests
{
    public class TransportComponentPerformanceTests
    {
        private readonly ITestOutputHelper _output;

        public TransportComponentPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private TransportComponent CreateComponent()
        {
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5000);
            options.EnforceOrdering = true;
            options.LocalIP = "127.0.0.1";

            var component = new TransportComponent(options)
            {
                Logger = NullLogger.Instance
            };
            // Reset state - usually Start() does this but we want to test HandleSocketMessage directly.
            // We need to simulate Start() partially to init _currentMessageCons
            component.Start();
            return component;
        }

        private SocketMessage CreateMessage(int seq)
        {
            var tm = new TransportMessage(new EventSource(Guid.NewGuid(), "test"), 1, "data")
            {
                MessageId = Guid.NewGuid()
            };
            string json = JsonConvert.SerializeObject(tm);
            byte[] data = Encoding.UTF8.GetBytes(json);
            return new SocketMessage(data, seq);
        }

        [Fact]
        public void OrderedMessages_Performance()
        {
            var component = CreateComponent();
            int count = 10000;
            var messages = Enumerable.Range(1, count).Select(CreateMessage).ToList();

            var sw = Stopwatch.StartNew();
            foreach (var msg in messages)
            {
                component.HandleSocketMessage(msg);
            }
            sw.Stop();
            _output.WriteLine($"Ordered: {count} messages in {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void ReverseMessages_Performance()
        {
            var component = CreateComponent();
            int count = 10000;
            // 10000, 9999, ..., 2, 1
            var messages = Enumerable.Range(1, count).Reverse().Select(CreateMessage).ToList();

            var sw = Stopwatch.StartNew();
            foreach (var msg in messages)
            {
                component.HandleSocketMessage(msg);
            }
            sw.Stop();
            _output.WriteLine($"Reverse: {count} messages in {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void RandomMessages_Performance()
        {
            var component = CreateComponent();
            int count = 10000;
            var rnd = new Random(42);
            var messages = Enumerable.Range(1, count).OrderBy(x => rnd.Next()).Select(CreateMessage).ToList();

            var sw = Stopwatch.StartNew();
            foreach (var msg in messages)
            {
                component.HandleSocketMessage(msg);
            }
            sw.Stop();
            _output.WriteLine($"Random: {count} messages in {sw.ElapsedMilliseconds}ms");
        }
    }
}
