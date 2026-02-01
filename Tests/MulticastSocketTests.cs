using System;
using Xunit;
using Ubicomp.Utils.NET.Sockets;
using System.Net;

namespace Ubicomp.Utils.NET.Tests
{
    public class MulticastSocketTests
    {
        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            string groupAddress = "239.0.0.1";
            int port = 5000;
            int ttl = 1;

            var socket = new MulticastSocket(groupAddress, port, ttl);

            Assert.NotNull(socket);
        }

        [Fact]
        public void StartReceiving_ShouldThrowIfNoListener()
        {
            var socket = new MulticastSocket("239.0.0.2", 5001, 1);
            
            Assert.Throws<ApplicationException>(() => socket.StartReceiving());
        }

        [Fact]
        public void SendAndReceive_ShouldWork()
        {
            string groupAddress = "239.0.0.3";
            int port = 5002;
            int ttl = 0;

            var receiver = new MulticastSocket(groupAddress, port, ttl);
            string receivedMessage = null;
            var signal = new System.Threading.ManualResetEvent(false);

            receiver.OnNotifyMulticastSocketListener += (s, e) =>
            {
                if (e.Type == MulticastSocketMessageType.MessageReceived)
                {
                    byte[] data = (byte[])e.NewObject;
                    receivedMessage = System.Text.Encoding.ASCII.GetString(data);
                    signal.Set();
                }
            };

            receiver.StartReceiving();
            System.Threading.Thread.Sleep(500); // Wait for bind

            var sender = new MulticastSocket(groupAddress, port, ttl);
            string msg = "Hello World";
            sender.Send(msg);

            Assert.True(signal.WaitOne(2000), "Timed out waiting for message");
            Assert.Equal(msg, receivedMessage);
        }
    }
}
