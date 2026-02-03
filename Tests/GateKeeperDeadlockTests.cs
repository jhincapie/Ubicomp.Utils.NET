#nullable enable
using System;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
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

            // Reset _currentMessageCons to 1 via reflection for a clean test
            var currentConsField = typeof(TransportComponent).GetField("_currentMessageCons", BindingFlags.Instance | BindingFlags.NonPublic);
            var gateField = typeof(TransportComponent).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic);

            if (currentConsField != null && gateField != null)
            {
                var gateObj = gateField.GetValue(transport);
                if (gateObj != null)
                {
                    lock (gateObj)
                    {
                        currentConsField.SetValue(transport, 1);
                        Monitor.PulseAll(gateObj);
                    }
                }
            }

            var receivedEvent = new ManualResetEvent(false);
            int msgType = 888;
            transport.RegisterHandler<EmptyContent>(msgType, (c, ctx) => receivedEvent.Set());

            // Get the private handler method
            var handlerMethod = typeof(TransportComponent).GetMethod("socket_OnNotifyMulticastSocketListener", BindingFlags.Instance | BindingFlags.NonPublic);

            // 1. Send malformed message (invalid JSON) as Consecutive 1
            byte[] badData = Encoding.UTF8.GetBytes("{ invalid json }");
            var args1 = new NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType.MessageReceived, badData, 1);

            // 2. Send valid message as Consecutive 2
            var source = new EventSource(Guid.NewGuid(), "TestSource");
            var validMsg = new TransportMessage(source, msgType, new EmptyContent());

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new TransportMessageConverter());
            string validJson = JsonConvert.SerializeObject(validMsg, settings);

            byte[] validData = Encoding.UTF8.GetBytes(validJson);
            var args2 = new NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType.MessageReceived, validData, 2);

            // Ensure _socket is not null and is the sender to avoid early return in handler
            var socketField = typeof(TransportComponent).GetField("_socket", BindingFlags.Instance | BindingFlags.NonPublic);
            var mockSocket = new MulticastSocket(options);
            socketField?.SetValue(transport, mockSocket);

            // Act
            // Invoke handler for message 1 (malformed)
            handlerMethod?.Invoke(transport, new object[] { mockSocket, args1 });

            // Invoke handler for message 2 (valid)
            handlerMethod?.Invoke(transport, new object[] { mockSocket, args2 });

            // Assert
            bool received = receivedEvent.WaitOne(2000);
            Assert.True(received, "GateKeeper deadlocked after malformed message. Message 2 was never processed.");
        }
    }
}
