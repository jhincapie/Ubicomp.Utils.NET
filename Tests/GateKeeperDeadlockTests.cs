#nullable enable
using System;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Text.Json;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("SharedTransport")]
    public class GateKeeperDeadlockTests
    {
        private class EmptyContent
        {
        }

        [Fact]
        public void GateKeeper_ShouldNotDeadlock_OnMalformedMessage()
        {
            // Arrange
            var options = MulticastSocketOptions.WideAreaNetwork("239.0.0.1", 5000, 1);
            var transport = new TransportComponent(options);

            var receivedEvent = new ManualResetEvent(false);
            int msgType = 888;
            transport.RegisterHandler<EmptyContent>(msgType, (c, ctx) => receivedEvent.Set());

            // 1. Send malformed message (invalid JSON) as Consecutive 1
            byte[] badData = Encoding.UTF8.GetBytes("{ invalid json }");

            // 2. Send valid message as Consecutive 2
            var source = new EventSource(Guid.NewGuid(), "TestSource");
            var validMsg = new TransportMessage(source, msgType, new EmptyContent());

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var knownTypes = new System.Collections.Generic.Dictionary<int, Type>();
            jsonOptions.Converters.Add(new TransportMessageConverter(knownTypes));
            byte[] validData = JsonSerializer.SerializeToUtf8Bytes(validMsg, jsonOptions);

            // Act
            // Invoke handler for message 1 (malformed)
            transport.HandleSocketMessage(new SocketMessage(badData, 1));

            // Invoke handler for message 2 (valid)
            transport.HandleSocketMessage(new SocketMessage(validData, 2));

            // Assert
            bool received = receivedEvent.WaitOne(2000);
            Assert.True(received, "GateKeeper deadlocked after malformed message. Message 2 was never processed.");
        }
    }
}
