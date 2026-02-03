#nullable enable
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("SharedTransport")]
    public class AutoAckTests
    {
        private class TestContent { public string Text { get; set; } = ""; }

        [Fact]
        public async Task MessageContext_ShouldReflectRequestAck()
        {
            // Arrange
            int msgType = 1001;
            var receivedEvent = new ManualResetEvent(false);
            bool? requestAckValue = null;

            var options = MulticastSocketOptions.WideAreaNetwork("239.1.2.7", 5006, 1);
            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .IgnoreLocalMessages(false)
                .RegisterHandler<TestContent>(msgType, (content, context) => 
                {
                    requestAckValue = context.RequestAck;
                    receivedEvent.Set();
                })
                .Build();

            transport.Start();

            try
            {
                // Act
                transport.Send(new TestContent { Text = "Ping" }, new SendOptions { RequestAck = true, MessageType = msgType });

                // Assert
                bool received = receivedEvent.WaitOne(2000);
                Assert.True(received);
                Assert.True(requestAckValue, "MessageContext.RequestAck should be true");
            }
            finally
            {
                transport.Stop();
            }
        }

        [Fact]
        public async Task AutoSendAcks_ShouldAutomaticallySendAck()
        {
            // Arrange
            int msgType = 1002;
            var options1 = MulticastSocketOptions.WideAreaNetwork("239.1.2.8", 5007, 1);
            var options2 = MulticastSocketOptions.WideAreaNetwork("239.1.2.8", 5007, 1);

            // Transport A: Sends message with AckRequest
            var transportA = new TransportBuilder()
                .WithMulticastOptions(options1)
                .WithLocalSource("SourceA")
                .IgnoreLocalMessages(true)
                .Build();

            // Transport B: Receives message and should Auto-Ack
            var transportB = new TransportBuilder()
                .WithMulticastOptions(options2)
                .WithLocalSource("SourceB")
                .WithAutoSendAcks(true)
                .IgnoreLocalMessages(true)
                .RegisterHandler<TestContent>(msgType, (c, ctx) => { /* Just handle it */ })
                .Build();

            transportA.Start();
            transportB.Start();

            try
            {
                // Act
                var session = transportA.Send(new TestContent { Text = "Ping" }, new SendOptions { RequestAck = true, MessageType = msgType });

                // Assert
                bool ackReceived = await session.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.True(ackReceived, "Ack should have been automatically sent by Transport B");
                Assert.Contains(session.ReceivedAcks, s => s.ResourceName == "SourceB");
            }
            finally
            {
                transportA.Stop();
                transportB.Stop();
            }
        }

        [Fact]
        public async Task AutoSendAcks_Disabled_ShouldNotSendAck()
        {
            // Arrange
            int msgType = 1003;
            var options1 = MulticastSocketOptions.WideAreaNetwork("239.1.2.9", 5008, 1);
            var options2 = MulticastSocketOptions.WideAreaNetwork("239.1.2.9", 5008, 1);

            var transportA = new TransportBuilder()
                .WithMulticastOptions(options1)
                .WithLocalSource("SourceA")
                .Build();

            var transportB = new TransportBuilder()
                .WithMulticastOptions(options2)
                .WithLocalSource("SourceB")
                .WithAutoSendAcks(false) // DISABLED
                .RegisterHandler<TestContent>(msgType, (c, ctx) => { })
                .Build();

            transportA.Start();
            transportB.Start();

            try
            {
                // Act
                var session = transportA.Send(new TestContent { Text = "Ping" }, new SendOptions { RequestAck = true, MessageType = msgType });

                // Assert
                bool ackReceived = await session.WaitAsync(TimeSpan.FromSeconds(1));
                Assert.False(ackReceived, "Ack should NOT have been sent when AutoSendAcks is false");
            }
            finally
            {
                transportA.Stop();
                transportB.Stop();
            }
        }
    }
}
