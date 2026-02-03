#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    /// <summary>
    /// Unit tests for the <see cref="TransportComponent"/> class.
    /// </summary>
    [Collection("SharedTransport")]
    public class TransportComponentTests
    {
        private class TestContent
        {
            public string Data { get; set; } = string.Empty;
        }

        /// <summary>
        /// Validates that sending and receiving a message end-to-end works
        /// correctly.
        /// </summary>
        [Fact]
        public void SendAndReceive_EndToEnd_Works()
        {
            // Arrange
            int msgType = 999;
            var receivedEvent = new ManualResetEvent(false);
            TestContent? receivedContent = null;
            MessageContext? receivedContext = null;

            var options = MulticastSocketOptions.WideAreaNetwork("239.1.2.6", 5005, 1);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                options.LocalIP = "127.0.0.1";
            }

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .IgnoreLocalMessages(false)
                .RegisterHandler<TestContent>(msgType, (content, context) =>
                {
                    receivedContent = content;
                    receivedContext = context;
                    receivedEvent.Set();
                })
                .Build();

            transport.Start();

            try
            {
                var content = new TestContent { Data = "Test Payload" };

                // Act
                transport.Send(content, new SendOptions { MessageType = msgType });

                // Assert
                bool received = receivedEvent.WaitOne(5000);

                Assert.True(received, "Message was not received by handler");
                Assert.NotNull(receivedContent);
                Assert.Equal("Test Payload", receivedContent!.Data);
                Assert.NotNull(receivedContext);
            }
            finally
            {
                transport.Stop();
            }
        }
    }
}
