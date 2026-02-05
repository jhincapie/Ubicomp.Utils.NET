using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class InMemorySocketTests
    {
        [MessageType("test.hello")]
        public class HelloMessage
        {
            public string Text { get; set; } = string.Empty;
        }

        [Fact]
        public async Task Test_TransportCommunication_InMemory()
        {
            // Arrange
            string groupAddr = "239.0.0.1";
            int port = 5000;

            // Create two InMemory sockets on the SAME "bus" (same group/port)
            var socket1 = new InMemoryMulticastSocket(groupAddr, port);
            var socket2 = new InMemoryMulticastSocket(groupAddr, port);

            var options = MulticastSocketOptions.LocalNetwork(groupAddr, port);

            var transport1 = new TransportComponent(options, socket1);
            var transport2 = new TransportComponent(options, socket2);

            transport1.LocalSource = new EventSource(Guid.NewGuid(), "Transport1");
            transport2.LocalSource = new EventSource(Guid.NewGuid(), "Transport2");

            transport1.RegisterMessageType<HelloMessage>("test.hello");

            var tcs = new TaskCompletionSource<HelloMessage>();
            transport2.RegisterHandler<HelloMessage>("test.hello", (msg, ctx) =>
            {
                tcs.TrySetResult(msg);
            });

            // Act
            transport1.Start();
            transport2.Start();

            await transport1.SendAsync(new HelloMessage { Text = "Hello Memory!" });

            // Assert
            var task = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.True(task == tcs.Task, "Message not received within timeout.");
            var resultMsg = await tcs.Task;
            Assert.Equal("Hello Memory!", resultMsg.Text);

            // Cleanup
            transport1.Stop();
            transport2.Stop();
        }

        [Fact]
        public async Task Test_JoinedAddresses()
        {
             string groupAddr = "239.0.0.1";
             int port = 5001;
             var socket = new InMemoryMulticastSocket(groupAddr, port);

             socket.StartReceiving();

             Assert.Single(socket.JoinedAddresses); // Should have the virtual 127.x.x.x

             socket.Close();
        }
    }
}
