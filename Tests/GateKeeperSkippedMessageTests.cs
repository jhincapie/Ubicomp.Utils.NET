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
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5000);
            var transport = new TransportComponent(options);
            transport.EnforceOrdering = true;
            transport.GateKeeperTimeout = TimeSpan.FromMilliseconds(200);

            // Start the transport (actor loops need to be running)
            try
            {
                transport.Start();
            }
            catch (Exception) { /* Socket bind might fail in test, ignore */ }

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

            // Sign the message manually (since we bypass SendAsync)
            transportMsg.Signature = transport.ComputeSignature(transportMsg, null);

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            string json = System.Text.Json.JsonSerializer.Serialize(transportMsg, jsonOptions);
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

            // We skip message 1 and send message 2.
            var task = Task.Run(() =>
            {
                transport.HandleSocketMessage(new SocketMessage(data, 2));
            });

            // Assert
            bool received = msg2Processed.WaitOne(2000);
            transport.Stop();

            Assert.True(received, "GateKeeper hung because message 1 was skipped.");
        }

        [Fact]
        public void GateKeeper_ShouldProcessImmediately_WhenOrderingDisabled()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5000);
            var transport = new TransportComponent(options);
            transport.EnforceOrdering = false;

            try
            {
                transport.Start();
            }
            catch (Exception) { /* Socket bind might fail in test, ignore */ }


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

            // Sign the message manually
            transportMsg.Signature = transport.ComputeSignature(transportMsg, null);

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            string json = System.Text.Json.JsonSerializer.Serialize(transportMsg, jsonOptions);
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

            // We send message 2 immediately. It should be processed without waiting for 1.
            transport.HandleSocketMessage(new SocketMessage(data, 2));

            // Assert
            bool received = msg2Processed.WaitOne(100); // Should be almost instant
            transport.Stop();
            Assert.True(received, "Message 2 was not processed immediately when ordering was disabled.");
        }
    }
}
