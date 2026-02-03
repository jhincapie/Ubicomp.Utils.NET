#nullable enable
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        [Fact]
        [Trait("Category", "Diagnostic")]
        public void A_FirewallCheck()
        {
            using var factory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var logger = factory.CreateLogger("FirewallCheck");

            logger.LogInformation("--- Firewall Diagnostic for Port {0} ---", TestPort);
            Ubicomp.Utils.NET.MulticastTransportFramework.NetworkDiagnostics.LogFirewallStatus(TestPort, logger);
            logger.LogInformation("-----------------------------------------");

            Assert.True(true);
        }

        [Fact]
        public void Builder_ShouldInitializeCorrectly()
        {
            var socket = new MulticastSocketBuilder()
                .WithWideAreaNetwork("239.0.0.10", TestPort, 2)
                .OnMessageReceived(_ => { })
                .Build();

            Assert.NotNull(socket);
        }

        [Fact]
        public void Builder_ShouldApplyInterfaceFilter()
        {
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.13", TestPort + 3);
            options.InterfaceFilter = addr => IPAddress.IsLoopback(addr);

            var socket = new MulticastSocketBuilder()
                .WithOptions(options)
                .OnMessageReceived(_ => { })
                .Build();

            Assert.NotNull(socket);
            foreach (var joined in socket.JoinedAddresses)
            {
                Assert.True(IPAddress.IsLoopback(joined), $"Non-loopback address joined: {joined}");
            }
        }

        [Fact]
        public async Task SendAndReceive_ShouldWork()
        {
            string groupAddress = "239.0.0.3";
            int port = TestPort + 10;
            int ttl = 0;

            string? localIP = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) ? "127.0.0.1" : null;

            var receiverOptions = MulticastSocketOptions.WideAreaNetwork(groupAddress, port, ttl);
            receiverOptions.LocalIP = localIP;

            string? receivedMessage = null;
            var signal = new ManualResetEvent(false);

            using var receiver = new MulticastSocketBuilder()
                .WithOptions(receiverOptions)
                .OnMessageReceived(msg =>
                {
                    receivedMessage = Encoding.UTF8.GetString(msg.Data);
                    signal.Set();
                })
                .Build();

            receiver.StartReceiving();
            Thread.Sleep(500);

            var senderOptions = MulticastSocketOptions.WideAreaNetwork(groupAddress, port, ttl);
            senderOptions.LocalIP = localIP;

            using var sender = new MulticastSocketBuilder()
                .WithOptions(senderOptions)
                .Build();

            string msgStr = "Hello World";
            await sender.SendAsync(msgStr);

            Assert.True(signal.WaitOne(2000), "Timed out waiting for message");
            Assert.Equal(msgStr, receivedMessage);
        }

        [Fact]
        public async Task Send_UTF8Message_ReceivedCorrectly()
        {
            string ip = "239.1.2.3";
            int port = TestPort + 11;
            string testMessage = "Héllø Wørld 🛡️";
            string? receivedMessage = null;
            var receivedEvent = new ManualResetEvent(false);

            string? localIP = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) ? "127.0.0.1" : null;
            var options = MulticastSocketOptions.LocalNetwork(ip, port);
            options.LocalIP = localIP;

            using var socket = new MulticastSocketBuilder()
                .WithOptions(options)
                .OnMessageReceived(msg =>
                {
                    receivedMessage = Encoding.UTF8.GetString(msg.Data);
                    receivedEvent.Set();
                })
                .Build();

            socket.StartReceiving();

            await socket.SendAsync(testMessage);
            bool signalReceived = receivedEvent.WaitOne(2000);

            Assert.True(signalReceived, "Timeout waiting for message");
            Assert.Equal(testMessage, receivedMessage);
        }
    }
}
