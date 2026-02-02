#nullable enable
using System;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
        [Collection("SharedTransport")]
        public class GateKeeperDeadlockTests
        {
            private class TestListener : ITransportListener
            {
                public ManualResetEvent ReceivedEvent { get; } = new ManualResetEvent(false);
                public TransportMessage? LastMessage { get; private set; }
    
                public void MessageReceived(TransportMessage message, string rawMessage)
                {
                    LastMessage = message;
                    ReceivedEvent.Set();
                }
            }
    
            private class EmptyContent : ITransportMessageContent { }
    
            [Fact]
            public void GateKeeper_ShouldNotDeadlock_OnMalformedMessage()
            {
                // Arrange
                var transport = TransportComponent.Instance;
                
                // Ensure transport is initialized to set _currentMessageCons = 1
                // We use dummy values since we won't actually use the socket
                transport.MulticastGroupAddress = IPAddress.Parse("239.0.0.1");
                transport.Port = 5000;
                try { transport.Init(); } catch { /* Ignore if already init */ }
    
                // Reset _currentMessageCons to 1 via reflection for a clean test
                var currentConsField = typeof(TransportComponent).GetField("_currentMessageCons", BindingFlags.Instance | BindingFlags.NonPublic);
                currentConsField?.SetValue(transport, 1);
    
                var listener = new TestListener();
                int msgType = 888;
                transport.TransportListeners[msgType] = listener;
                TransportMessageConverter.KnownTypes[msgType] = typeof(EmptyContent);
    
                // Get the private handler method
                var handlerMethod = typeof(TransportComponent).GetMethod("socket_OnNotifyMulticastSocketListener", BindingFlags.Instance | BindingFlags.NonPublic);
    

        

                    // 1. Send malformed message (invalid JSON) as Consecutive 1

                    byte[] badData = Encoding.UTF8.GetBytes("{ invalid json }");

                    var args1 = new NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType.MessageReceived, badData, 1);

                    

                    // 2. Send valid message as Consecutive 2

                    var source = new EventSource(Guid.NewGuid(), "TestSource");

                    var validMsg = new TransportMessage(source, msgType, new EmptyContent());

                    string validJson = transport.Send(validMsg); // This just gets the JSON, we'll pass it manually

        
            byte[] validData = Encoding.UTF8.GetBytes(validJson);
            var args2 = new NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType.MessageReceived, validData, 2);

            // Act
            // Invoke handler for message 1 (malformed)
            handlerMethod?.Invoke(transport, new object[] { this, args1 });

            // Invoke handler for message 2 (valid)
            handlerMethod?.Invoke(transport, new object[] { this, args2 });

            // Assert
            bool received = listener.ReceivedEvent.WaitOne(2000);
            Assert.True(received, "GateKeeper deadlocked after malformed message. Message 2 was never processed.");
        }
    }
}
