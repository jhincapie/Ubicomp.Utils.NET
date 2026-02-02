#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Xunit;

using System.Text;
using System.Reflection;
using Ubicomp.Utils.NET.Sockets;

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
            settings.Converters.Add(new TransportMessageConverter());
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
            session.OnAckReceived += (s, src) => {
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
        public void TransportComponent_Send_ShouldReturnAckSession()
        {
            var tc = TransportComponent.Instance;
            var msg = new TransportMessage { RequestAck = true };
            
            var session = tc.Send(msg);
            
            Assert.NotNull(session);
            Assert.Equal(msg.MessageId, session.OriginalMessageId);
        }

        [Fact]
        public async Task TransportComponent_AckProcessing_Simulation()
        {
            // Arrange
            var tc = TransportComponent.Instance;
            tc.IgnoreLocalMessages = false;
            
            // Reflection to setup tc
            var socketField = typeof(TransportComponent).GetField("_socket", BindingFlags.Instance | BindingFlags.NonPublic);
            var mockSocket = new MulticastSocket(MulticastSocketOptions.WideAreaNetwork("239.0.0.1", 5000, 1));
            socketField?.SetValue(tc, mockSocket);

            var currentConsField = typeof(TransportComponent).GetField("_currentMessageCons", BindingFlags.Instance | BindingFlags.NonPublic);
            var gateField = typeof(TransportComponent).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic);
            if (currentConsField != null && gateField != null)
            {
                var gateObj = gateField.GetValue(tc);
                if (gateObj != null)
                {
                    lock (gateObj)
                    {
                        currentConsField.SetValue(tc, 1);
                        Monitor.PulseAll(gateObj);
                    }
                }
            }

            var handlerMethod = typeof(TransportComponent).GetMethod("socket_OnNotifyMulticastSocketListener", BindingFlags.Instance | BindingFlags.NonPublic);

            var msg = new TransportMessage { RequestAck = true };
            var session = tc.Send(msg);

            // Create an Ack message for this msg
            var ackContent = new AckMessageContent { OriginalMessageId = msg.MessageId };
            var ackSource = new EventSource(Guid.NewGuid(), "Responder");
            var ackMsg = new TransportMessage(ackSource, TransportComponent.AckMessageType, ackContent);

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new TransportMessageConverter());
            string ackJson = JsonConvert.SerializeObject(ackMsg, settings);
            byte[] ackData = Encoding.UTF8.GetBytes(ackJson);

            var args = new NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType.MessageReceived, ackData, 1);

            // Act
            handlerMethod?.Invoke(tc, new object[] { mockSocket, args });

            // Assert
            var result = await session.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result, "Ack was not processed correctly in simulation");
            Assert.True(session.IsAnyAckReceived);
            Assert.Contains(ackSource.ResourceId, session.ReceivedAcks.Select(s => s.ResourceId));
        }

        [Fact]
        public void TransportComponent_IgnoreLocalMessages_ShouldFilter()
        {
            // Arrange
            var tc = TransportComponent.Instance;
            bool originalValue = tc.IgnoreLocalMessages;
            tc.IgnoreLocalMessages = true;
            
            // Reflection to setup tc
            var socketField = typeof(TransportComponent).GetField("_socket", BindingFlags.Instance | BindingFlags.NonPublic);
            var mockSocket = new MulticastSocket(MulticastSocketOptions.WideAreaNetwork("239.0.0.1", 5000, 1));
            socketField?.SetValue(tc, mockSocket);

            var currentConsField = typeof(TransportComponent).GetField("_currentMessageCons", BindingFlags.Instance | BindingFlags.NonPublic);
            var gateField = typeof(TransportComponent).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic);
            if (currentConsField != null && gateField != null)
            {
                var gateObj = gateField.GetValue(tc);
                if (gateObj != null)
                {
                    lock (gateObj)
                    {
                        currentConsField.SetValue(tc, 1);
                        Monitor.PulseAll(gateObj);
                    }
                }
            }

            var handlerMethod = typeof(TransportComponent).GetMethod("socket_OnNotifyMulticastSocketListener", BindingFlags.Instance | BindingFlags.NonPublic);

            // Create a message from LocalSource
            var msg = new TransportMessage(tc.LocalSource, TransportComponent.TransportComponentID, new AckMessageContent());
            
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new TransportMessageConverter());
            string json = JsonConvert.SerializeObject(msg, settings);
            byte[] data = Encoding.UTF8.GetBytes(json);

            var args = new NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType.MessageReceived, data, 1);

            // Register a listener to see if it gets called
            bool listenerCalled = false;
            var listener = new MockListener(() => listenerCalled = true);
            tc.TransportListeners[TransportComponent.TransportComponentID] = listener;

            // Act
            handlerMethod?.Invoke(tc, new object[] { mockSocket, args });

            // Assert
            Assert.False(listenerCalled, "Listener should not have been called for a local message when IgnoreLocalMessages is true");
            
            // Cleanup
            tc.IgnoreLocalMessages = originalValue;
        }

        private class MockListener : ITransportListener
        {
            private readonly Action _onReceived;
            public MockListener(Action onReceived) => _onReceived = onReceived;
            public void MessageReceived(TransportMessage message, string rawMessage) => _onReceived();
        }
    }
}
