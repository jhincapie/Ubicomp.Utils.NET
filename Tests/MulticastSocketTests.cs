using System;
using System.Text;
using System.Threading;
using System.IO;
using Xunit;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.Tests
{
    public class MulticastSocketTests
    {
        [Fact]
        public void TestInitialization_HandlesNoDelayException()
        {
            // Should not throw
            var socket = new MulticastSocket("224.0.0.1", 5000, 1);
            Assert.NotNull(socket);
        }

        [Fact]
        public void TestExceptionLogging_LogsToConsoleError()
        {
            var sb = new StringBuilder();
            var originalError = Console.Error;
            var writer = new StringWriter(sb);
            Console.SetError(writer);

            try
            {
                var socket = new MulticastSocket("224.0.0.1", 5001, 1);
                var evt = new ManualResetEvent(false);

                socket.OnNotifyMulticastSocketListener += (s, e) =>
                {
                    if (e.Type == MulticastSocketMessageType.MessageSent)
                    {
                        throw new Exception("Test Exception for Logging");
                    }
                };

                socket.Send("trigger");

                // Wait for async processing
                // We can't easily wait for the exception to be caught and logged.
                // So we sleep a reasonable amount.
                Thread.Sleep(1000);

                var output = sb.ToString();
                Assert.Contains("Test Exception for Logging", output);
            }
            finally
            {
                Console.SetError(originalError);
            }
        }

        [Fact]
        public void TestSend_UsesUTF8()
        {
             var ip = "224.0.0.1";
             var port = 5002;
             var socket = new MulticastSocket(ip, port, 1);
             var received = new ManualResetEvent(false);
             byte[] receivedBytes = null;

             socket.OnNotifyMulticastSocketListener += (s, e) =>
             {
                 if (e.Type == MulticastSocketMessageType.MessageReceived)
                 {
                     receivedBytes = (byte[])e.NewObject;
                     received.Set();
                 }
             };

             socket.StartReceiving();

             string message = "Hello 🌍";
             socket.Send(message);

             Assert.True(received.WaitOne(2000), "Timed out waiting for message");

             byte[] expected = Encoding.UTF8.GetBytes(message);
             Assert.Equal(expected, receivedBytes);
        }
    }
}
