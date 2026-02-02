#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    /// <summary>
    /// Unit tests for the <see cref="TransportComponent"/> class.
    /// </summary>
    [Collection("SharedTransport")]
    public class TransportComponentTests
    {
        private class TestListener : ITransportListener
        {
            public ManualResetEvent ReceivedEvent
            {
                get;
            } = new ManualResetEvent(false);
            public TransportMessage? LastMessage
            {
                get; private set;
            }

            /// <inheritdoc />
            public void MessageReceived(TransportMessage message,
                                        string rawMessage)
            {
                LastMessage = message;
                ReceivedEvent.Set();
            }
        }

        private class TestContent : ITransportMessageContent
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
            var transport = TransportComponent.Instance;

            // Register known type for deserialization
            int msgType = 999;
            if (!TransportMessageConverter.KnownTypes.ContainsKey(msgType))
                TransportMessageConverter.KnownTypes.Add(msgType,
                                                         typeof(TestContent));

            // Setup Transport (Singleton)
            // Use a port that is likely free
            int port = 5000;
            transport.MulticastGroupAddress = IPAddress.Parse("239.1.2.6");
            transport.Port = port;
            transport.UDPTTL = 1;

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.Linux))
            {
                transport.LocalIPAddress = IPAddress.Loopback;
            }
            else
            {
                transport.LocalIPAddress = null;
            }

            try
            {
                transport.Init();
            }
            catch (Exception)
            {
                // Ignore if already initialized
            }

            var listener = new TestListener();

            // Register listener
            if (transport.TransportListeners.ContainsKey(msgType))
                transport.TransportListeners.Remove(msgType);
            transport.TransportListeners.Add(msgType, listener);

            var content = new TestContent { Data = "Test Payload" };
            var source = new EventSource(Guid.NewGuid(), "TestSource");
            var msg = new TransportMessage(source, msgType, content);

            // Act
            transport.Send(msg);

            // Assert
            // Wait for message to go through Socket -> OnNotify -> GateKeeper
            // -> MessageReceived
            bool received = listener.ReceivedEvent.WaitOne(5000);

            Assert.True(received, "Message was not received by listener - " +
                                      "GateKeeper might be stuck");
            Assert.NotNull(listener.LastMessage);
            Assert.Equal(msgType, listener.LastMessage!.MessageType);

            var receivedContent =
                listener.LastMessage.MessageData as TestContent;
            Assert.NotNull(receivedContent);
            Assert.Equal("Test Payload", receivedContent!.Data);
        }
    }
}
