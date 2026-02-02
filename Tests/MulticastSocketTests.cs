#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    /// <summary>
    /// Unit tests for the <see cref="MulticastSocket"/> class.
    /// </summary>
    [Collection("SharedTransport")]
    public class MulticastSocketTests
    {
        private const int TestPort = 5000;

        /// <summary>
        /// Diagnostic test to report firewall status before other tests.
        /// </summary>
        [Fact]
        [Trait("Category", "Diagnostic")]
        public void A_FirewallCheck()
        {
            // We name it starting with A_ to encourage alphabetical order in
            // some runners, or just rely on it being the first one.
            using var factory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var logger = factory.CreateLogger("FirewallCheck");
            
            logger.LogInformation("--- Firewall Diagnostic for Port {0} ---", TestPort);
            Ubicomp.Utils.NET.MulticastTransportFramework.NetworkDiagnostics.LogFirewallStatus(TestPort, logger);
            logger.LogInformation("-----------------------------------------");
            
            // This test is for reporting, so we just pass unless we want to
            // enforce it.
            Assert.True(true);
        }

        /// <summary>
        /// Validates that the constructor initializes the socket correctly.
        /// </summary>
        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            string groupAddress = "239.0.0.1";
            int port = TestPort;
            int ttl = 1;

            var socket = new MulticastSocket(groupAddress, port, ttl);

            Assert.NotNull(socket);
        }

        /// <summary>
        /// Validates that StartReceiving throws an exception if no listener is
        /// registered.
        /// </summary>
        [Fact]
        public void StartReceiving_ShouldThrowIfNoListener()
        {
            var socket = new MulticastSocket("239.0.0.2", TestPort, 1);

            Assert.Throws<ApplicationException>(() => socket.StartReceiving());
        }

        /// <summary>
        /// Validates that sending and receiving messages via the socket works
        /// correctly.
        /// </summary>
        [Fact]
        public void SendAndReceive_ShouldWork()
        {
            string groupAddress = "239.0.0.3";
            int port = TestPort;
            int ttl = 0;

            // Use loopback on Linux for reliable local tests without firewall
            // interference
            string? localIP =
                System.Runtime.InteropServices.RuntimeInformation
                        .IsOSPlatform(System.Runtime.InteropServices
                                          .OSPlatform.Linux)
                    ? "127.0.0.1"
                    : null;

            using var receiver =
                new MulticastSocket(groupAddress, port, ttl, localIP);
            string? receivedMessage = null;
            var signal = new ManualResetEvent(false);

            receiver.OnNotifyMulticastSocketListener += (s, e) =>
            {
                if (e.Type == MulticastSocketMessageType.MessageReceived)
                {
                    byte[] data = (byte[])e.NewObject!;
                    receivedMessage = Encoding.ASCII.GetString(data);
                    signal.Set();
                }
            };

            receiver.StartReceiving();
            Thread.Sleep(500); // Wait for bind

            using var sender =
                new MulticastSocket(groupAddress, port, ttl, localIP);
            string msg = "Hello World";
            sender.Send(msg);

            Assert.True(signal.WaitOne(2000), "Timed out waiting for message");
            Assert.Equal(msg, receivedMessage);
        }

        /// <summary>
        /// Validates that UTF-8 messages are correctly received and decoded.
        /// </summary>
        [Fact]
        public void Send_UTF8Message_ReceivedCorrectly()
        {
            // Arrange
            string ip = "239.1.2.3";
            int port = TestPort;
            int ttl = 1;
            string testMessage = "Héllø Wørld 🛡️";
            string? receivedMessage = null;
            var receivedEvent = new ManualResetEvent(false);

            // Use 127.0.0.1 for loopback tests on Linux/multi-homed systems
            var socket = new MulticastSocket(ip, port, ttl, "127.0.0.1");

            socket.OnNotifyMulticastSocketListener += (sender, e) =>
            {
                Console.WriteLine($"Test Event: {e.Type}");
                if (e.Type == MulticastSocketMessageType.MessageReceived)
                {
                    byte[] receivedBytes = (byte[])e.NewObject!;
                    // Decoding using UTF8 as per our fix
                    receivedMessage =
                        Encoding.UTF8.GetString(receivedBytes).Trim('\0');
                    receivedEvent.Set();
                }
                else if (e.Type ==
                             MulticastSocketMessageType.ReceiveException ||
                         e.Type == MulticastSocketMessageType.SendException)
                {
                    Console.WriteLine(
                        $"Socket Exception in test: {e.NewObject}");
                }
            };

            try
            {
                socket.StartReceiving();
            }
            catch (SocketException)
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

        /// <summary>
        /// Validates that exceptions in listeners are logged to the console
        /// error stream.
        /// </summary>
        [Fact]
        public void Listener_Exception_IsLoggedToConsoleError()
        {
            // Arrange
            string ip = "239.1.2.4";
            int port = TestPort;
            int ttl = 1;

            // Use 127.0.0.1 for loopback tests on Linux/multi-homed systems
            var socket = new MulticastSocket(ip, port, ttl, "127.0.0.1");

            // Capture Console.Error
            var stringWriter = new StringWriter();
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
                catch (SocketException)
                {
                }

                // Act
                socket.Send("Trigger");

                // We need to wait a bit because the log happens in the catch
                // block of the background thread
                int retries = 10;
                while (retries > 0)
                {
                    if (stringWriter.ToString().Contains(
                            "Test Exception Swallowing"))
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
