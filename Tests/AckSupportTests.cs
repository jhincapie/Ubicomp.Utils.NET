#nullable enable
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("SharedTransport")]
    public class AckSupportTests
    {
        [Fact]
        public void AckMessageContent_ShouldStoreOriginalMessageId()
        {
            var originalId = Guid.NewGuid();
            var ackContent = new AckMessageContent { OriginalMessageId = originalId };
            Assert.Equal(originalId, ackContent.OriginalMessageId);
        }

        [Fact]
        public void TransportMessage_RequestAck_ShouldSerializeOnlyWhenTrue()
        {
            var msg = new TransportMessage();

            // Default should be false
            Assert.False(msg.RequestAck);

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new TransportMessageConverter(new System.Collections.Generic.Dictionary<int, Type>()));
            var jsonFalse = JsonConvert.SerializeObject(msg, settings);
            Assert.DoesNotContain("RequestAck", jsonFalse);

            msg.RequestAck = true;
            var jsonTrue = JsonConvert.SerializeObject(msg, settings);
            Assert.Contains("\"requestAck\":true", jsonTrue);
        }

        [Fact]
        public async Task AckSession_ShouldCompleteWhenAckReceived()
        {
            var originalId = Guid.NewGuid();
            var session = new AckSession(originalId);

            var source = new EventSource(Guid.NewGuid(), "Tester", "127.0.0.1");

            bool eventFired = false;
            session.OnAckReceived += (s, src) =>
            {
                eventFired = true;
                Assert.Equal(source, src);
            };

            // Simulate ack arrival
            session.ReportAck(source);

            var result = await session.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.True(result);
            Assert.True(session.IsAnyAckReceived);
            Assert.Contains(source, session.ReceivedAcks);
            Assert.True(eventFired);
        }

        [Fact]
        public async Task AckSession_ShouldTimeoutIfNoAckReceived()
        {
            var originalId = Guid.NewGuid();
            var session = new AckSession(originalId);

            var result = await session.WaitAsync(TimeSpan.FromMilliseconds(100));

            Assert.False(result);
            Assert.False(session.IsAnyAckReceived);
            Assert.Empty(session.ReceivedAcks);
        }

        [Fact]
        public async Task TransportComponent_Send_ShouldReturnAckSession()
        {
            var options = MulticastSocketOptions.WideAreaNetwork("239.0.0.1", 5000, 1);
            var tc = new TransportComponent(options);

            // Register a dummy type for sending
            tc.RegisterHandler<AckMessageContent>(1, (c, ctx) => { });

            var session = await tc.SendAsync(new AckMessageContent(), new SendOptions { RequestAck = true });

            Assert.NotNull(session);
        }

        [Fact]
        public async Task TransportComponent_AckProcessing_Simulation()
        {
            // Arrange
            var options = MulticastSocketOptions.WideAreaNetwork("239.0.0.1", 5000, 1);
            var tc = new TransportComponent(options);
            tc.IgnoreLocalMessages = false;

            // Register a dummy type for sending
            tc.RegisterHandler<AckMessageContent>(1, (c, ctx) => { });
            var session = await tc.SendAsync(new AckMessageContent(), new SendOptions { RequestAck = true });

            // Create an Ack message for this msg
            var manualSession = await tc.SendAsync(new AckMessageContent(), new SendOptions { RequestAck = true });

            var ackContent = new AckMessageContent { OriginalMessageId = manualSession.OriginalMessageId };
            var ackSource = new EventSource(Guid.NewGuid(), "Responder");
            var ackMsg = new TransportMessage(ackSource, TransportComponent.AckMessageType, ackContent);

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new TransportMessageConverter(new System.Collections.Generic.Dictionary<int, Type>()));
            string ackJson = JsonConvert.SerializeObject(ackMsg, settings);
            byte[] ackData = Encoding.UTF8.GetBytes(ackJson);

            // Act
            tc.HandleSocketMessage(new SocketMessage(ackData, 1));

            // Assert
            var result = await manualSession.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result, "Ack was not processed correctly in simulation");
            Assert.True(manualSession.IsAnyAckReceived);
            Assert.Contains(ackSource.ResourceId, manualSession.ReceivedAcks.Select(s => s.ResourceId));
        }

        [Fact]
        public void TransportComponent_IgnoreLocalMessages_ShouldFilter()
        {
            // Arrange
            var options = MulticastSocketOptions.WideAreaNetwork("239.0.0.1", 5000, 1);
            var tc = new TransportComponent(options);
            tc.IgnoreLocalMessages = true;

            // Create a message from LocalSource
            int msgType = 123;
            var content = new AckMessageContent();
            var msg = new TransportMessage(tc.LocalSource, msgType, content);

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new TransportMessageConverter(new System.Collections.Generic.Dictionary<int, Type>()));
            string json = JsonConvert.SerializeObject(msg, settings);
            byte[] data = Encoding.UTF8.GetBytes(json);

            // Register a handler to see if it gets called
            bool handlerCalled = false;
            tc.RegisterHandler<AckMessageContent>(msgType, (c, ctx) => handlerCalled = true);

            // Act
            tc.HandleSocketMessage(new SocketMessage(data, 1));

            // Assert
            Assert.False(handlerCalled, "Handler should not have been called for a local message when IgnoreLocalMessages is true");
        }
    }
}
