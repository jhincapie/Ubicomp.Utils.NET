using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Xunit;
using Ubicomp.Utils.NET.Sockets;

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
      
        [Fact]
        public void Send_UTF8Message_ReceivedCorrectly()
        {
            // Arrange
            string ip = "239.1.2.3";
            int port = 5001;
            int ttl = 1;
            string testMessage = "Héllø Wørld 🛡️";
            string receivedMessage = null;
            ManualResetEvent receivedEvent = new ManualResetEvent(false);

            // Use 127.0.0.1 for loopback tests on Linux/multi-homed systems
            MulticastSocket socket = new MulticastSocket(ip, port, ttl, "127.0.0.1");

            socket.OnNotifyMulticastSocketListener += (sender, e) =>
            {
                Console.WriteLine($"Test Event: {e.Type}");
                if (e.Type == MulticastSocketMessageType.MessageReceived)
                {
                    byte[] receivedBytes = (byte[])e.NewObject;
                    // Decoding using UTF8 as per our fix
                    receivedMessage = Encoding.UTF8.GetString(receivedBytes).Trim('\0');
                    receivedEvent.Set();
                }
                else if (e.Type == MulticastSocketMessageType.ReceiveException || e.Type == MulticastSocketMessageType.SendException)
                {
                    Console.WriteLine($"Socket Exception in test: {e.NewObject}");
                }
            };

            try
            {
                socket.StartReceiving();
            }
            catch(SocketException)
            {
                 // Ignore potential NoDelay error for the sake of this test
            }

            // Act
            socket.Send(testMessage);
            bool signalReceived = receivedEvent.WaitOne(2000);

            // Assert
            Assert.True(signalReceived, "Timeout waiting for message");
            Assert.Equal(testMessage, receivedMessage);
        }

        [Fact]
        public void Listener_Exception_IsLoggedToConsoleError()
        {
            // Arrange
            string ip = "239.1.2.4";
            int port = 5002;
            int ttl = 1;
            
            // Use 127.0.0.1 for loopback tests on Linux/multi-homed systems
            MulticastSocket socket = new MulticastSocket(ip, port, ttl, "127.0.0.1");

            // Capture Console.Error
            StringWriter stringWriter = new StringWriter();
            TextWriter originalError = Console.Error;
            Console.SetError(stringWriter);

            try
            {
                socket.OnNotifyMulticastSocketListener += (sender, e) =>
                {
                    if (e.Type == MulticastSocketMessageType.MessageReceived)
                    {
                        throw new Exception("Test Exception Swallowing");
                    }
                };

                try
                {
                    socket.StartReceiving();
                }
                catch(SocketException){}

                // Act
                socket.Send("Trigger");

                // We need to wait a bit because the log happens in the catch block of the background thread
                int retries = 10;
                while (retries > 0)
                {
                    if (stringWriter.ToString().Contains("Test Exception Swallowing"))
                    {
                        break;
                    }
                    Thread.Sleep(200);
                    retries--;
                }

                // Assert
                string errorOutput = stringWriter.ToString();
                Assert.Contains("Test Exception Swallowing", errorOutput);
            }
            finally
            {
                Console.SetError(originalError);
                stringWriter.Dispose();
            }
        }
    }
}