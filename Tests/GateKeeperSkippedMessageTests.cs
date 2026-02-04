#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class GateKeeperSkippedMessageTests
    {
        [Fact]
        public void GateKeeper_ShouldNotHangForever_WhenMessageIsSkipped()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            options.EnforceOrdering = true;
            var transport = new TransportComponent(options);
            transport.GateKeeperTimeout = TimeSpan.FromMilliseconds(200);

            // We simulate messages arriving out of order or one being skipped.
            // Note: HandleSocketMessage is internal, so we can call it directly.

            var msg2Processed = new ManualResetEvent(false);
            string msgType = "test.skipped";
            transport.RegisterHandler<string>(msgType, (data, ctx) =>
            {
                if (data == "msg2")
                    msg2Processed.Set();
            });

            // Act
            var source = new EventSource(Guid.NewGuid(), "TestSource");
            var transportMsg = new TransportMessage(source, msgType, "msg2");
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(transportMsg, new Newtonsoft.Json.JsonSerializerSettings
            {
                Converters = { new TransportMessageConverter(new System.Collections.Generic.Dictionary<string, Type>()) }
            });
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

            // We skip message 1 and send message 2.
            var task = Task.Run(() =>
            {
                transport.HandleSocketMessage(new SocketMessage(data, 2));
            });

            // Assert
            bool received = msg2Processed.WaitOne(2000);

            Assert.True(received, "GateKeeper hung because message 1 was skipped.");
        }

        [Fact]
        public void GateKeeper_ShouldProcessImmediately_WhenOrderingDisabled()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            options.EnforceOrdering = false;
            var transport = new TransportComponent(options);

            var msg2Processed = new ManualResetEvent(false);
            string msgType = "test.skipped";
            transport.RegisterHandler<string>(msgType, (data, ctx) =>
            {
                if (data == "msg2")
                    msg2Processed.Set();
            });

            // Act
            var source = new EventSource(Guid.NewGuid(), "TestSource");
            var transportMsg = new TransportMessage(source, msgType, "msg2");
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(transportMsg, new Newtonsoft.Json.JsonSerializerSettings
            {
                Converters = { new TransportMessageConverter(new System.Collections.Generic.Dictionary<string, Type>()) }
            });
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

            // We send message 2 immediately. It should be processed without waiting for 1.
            transport.HandleSocketMessage(new SocketMessage(data, 2));

            // Assert
            bool received = msg2Processed.WaitOne(100); // Should be almost instant
            Assert.True(received, "Message 2 was not processed immediately when ordering was disabled.");
        }
    }
}
