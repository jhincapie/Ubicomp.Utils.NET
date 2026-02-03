#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
        /// correctly using the asynchronous API.
        /// </summary>
        [Fact]
        public async Task SendAndReceive_EndToEnd_Works()
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
                await transport.SendAsync(content, new SendOptions { MessageType = msgType });

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

        /// <summary>
        /// Validates that sending and receiving a message asynchronously
        /// end-to-end works correctly.
        /// </summary>
        [Fact]
        public async Task SendAsync_EndToEnd_Works()
        {
            // Arrange
            int msgType = 1000;
            var receivedEvent = new TaskCompletionSource<bool>();
            TestContent? receivedContent = null;

            var options = MulticastSocketOptions.WideAreaNetwork("239.1.2.7", 5006, 1);
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
                    receivedEvent.TrySetResult(true);
                })
                .Build();

            transport.Start();

            try
            {
                var content = new TestContent { Data = "Async Test Payload" };

                // Act
                var session = await transport.SendAsync(content, new SendOptions { MessageType = msgType });

                // Assert
                Assert.NotNull(session);

                var received = await Task.WhenAny(receivedEvent.Task, Task.Delay(5000)) == receivedEvent.Task;

                Assert.True(received, "Message was not received by handler");
                Assert.NotNull(receivedContent);
                Assert.Equal("Async Test Payload", receivedContent!.Data);
            }
            finally
            {
                transport.Stop();
            }
        }
    }
}
