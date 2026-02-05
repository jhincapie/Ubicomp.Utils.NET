#nullable enable
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("SharedTransport")]
    public class BufferLimitTests
    {
        private const int TestPort = 5500;

        [Fact]
        public async Task SendLargeMessage_ShouldBeReceived()
        {
            // Arrange
            string groupAddress = "239.0.0.55";
            int port = TestPort;
            string? localIP = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) ? "127.0.0.1" : null;

            var options = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            options.LocalIP = localIP;

            string largeMessage = new string('A', 2000); // 2000 characters > 1024 bytes
            string? receivedMessage = null;
            var signal = new ManualResetEvent(false);

            using var socket = new MulticastSocketBuilder()
                .WithOptions(options)
                .OnMessageReceived(msg =>
                {
                    receivedMessage = Encoding.UTF8.GetString(msg.Data, 0, msg.Length);
                    msg.Dispose();
                    signal.Set();
                })
                .Build();

            socket.StartReceiving();
            await Task.Delay(500);

            // Act
            await socket.SendAsync(largeMessage);

            // Assert
            bool signalReceived = signal.WaitOne(2000);
            Assert.True(signalReceived, "Timed out waiting for large message. Likely dropped due to small buffer.");
            Assert.Equal(largeMessage.Length, receivedMessage?.Length);
        }

        [Fact]
        public async Task SendTooLargeMessage_ShouldThrowException()
        {
            // Arrange
            string groupAddress = "239.0.0.55";
            int port = TestPort + 1;

            var options = MulticastSocketOptions.LocalNetwork(groupAddress, port);

            using var socket = new MulticastSocketBuilder()
                .WithOptions(options)
                .Build();

            // Create a message slightly larger than the limit
            // MaxMessageSize is 65535, so 65536 should fail
            byte[] tooLargeBuffer = new byte[MulticastSocket.MaxMessageSize + 1];

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await socket.SendAsync(tooLargeBuffer);
            });
        }
    }
}
