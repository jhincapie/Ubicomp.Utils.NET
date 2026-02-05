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
        [MessageType("test.autoack.content")]
        private class TestContent
        {
            public string Text { get; set; } = "";
        }

        [Fact]
        public async Task MessageContext_ShouldReflectRequestAck()
        {
            // Arrange
            var receivedEvent = new ManualResetEvent(false);
            bool? requestAckValue = null;

            var options = MulticastSocketOptions.WideAreaNetwork("239.1.2.7", 5006, 1);

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .RegisterHandler<TestContent>((content, context) =>
                {
                    requestAckValue = context.RequestAck;
                    receivedEvent.Set();
                })
                .Build();

            transport.Start();

            try
            {
                // Act
                await transport.SendAsync(new TestContent { Text = "Ping" }, new SendOptions { RequestAck = true });

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
            var options1 = MulticastSocketOptions.WideAreaNetwork("239.1.2.8", 5007, 1);
            var options2 = MulticastSocketOptions.WideAreaNetwork("239.1.2.8", 5007, 1);

            // Transport A: Sends message with AckRequest
            var transportA = new TransportBuilder()
                .WithMulticastOptions(options1)
                .WithSocket(TestConfiguration.CreateSocket(options1))
                .WithLocalSource("SourceA")
                .Build();

            // Transport B: Receives message and should Auto-Ack
            var transportB = new TransportBuilder()
                .WithMulticastOptions(options2)
                .WithSocket(TestConfiguration.CreateSocket(options2))
                .WithLocalSource("SourceB")
                .WithAutoSendAcks(true)
                .RegisterHandler<TestContent>((c, ctx) => { /* Just handle it */ })
                .Build();

            transportA.Start();
            transportB.Start();

            try
            {
                // Act
                var session = await transportA.SendAsync(new TestContent { Text = "Ping" }, new SendOptions { RequestAck = true });

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
            var options1 = MulticastSocketOptions.WideAreaNetwork("239.1.2.9", 5008, 1);
            var options2 = MulticastSocketOptions.WideAreaNetwork("239.1.2.9", 5008, 1);

            var transportA = new TransportBuilder()
                .WithMulticastOptions(options1)
                .WithSocket(TestConfiguration.CreateSocket(options1))
                .WithLocalSource("SourceA")
                .Build();

            var transportB = new TransportBuilder()
                .WithMulticastOptions(options2)
                .WithSocket(TestConfiguration.CreateSocket(options2))
                .WithLocalSource("SourceB")
                .WithAutoSendAcks(false) // DISABLED
                .RegisterHandler<TestContent>((c, ctx) => { })
                .Build();

            transportA.Start();
            transportB.Start();

            try
            {
                // Act
                var session = await transportA.SendAsync(new TestContent { Text = "Ping" }, new SendOptions { RequestAck = true });

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
