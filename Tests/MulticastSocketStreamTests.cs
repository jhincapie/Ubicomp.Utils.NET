#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class MulticastSocketStreamTests
    {
        [Fact]
        public async Task GetMessageStream_ShouldReceiveMessages()
        {
            // Arrange
            string groupAddress = "239.0.0.50";
            int port = 5100;
            var options = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            
            // For tests, use loopback if on Linux
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                options.LocalIP = "127.0.0.1";
            }

            using var receiver = new MulticastSocketBuilder()
                .WithOptions(options)
                .Build();

            using var sender = new MulticastSocketBuilder()
                .WithOptions(options)
                .Build();

            receiver.StartReceiving();
            
            var cts = new CancellationTokenSource();
            var stream = receiver.GetMessageStream(cts.Token);

            string testMessage = "Stream Test";
            
            // Act
            await sender.SendAsync(testMessage);

            // Assert
            var enumerator = stream.GetAsyncEnumerator();
            bool hasMessage = await enumerator.MoveNextAsync();
            
            Assert.True(hasMessage);
            Assert.NotNull(enumerator.Current);
            Assert.Equal(testMessage, Encoding.UTF8.GetString(enumerator.Current.Data));
            
            cts.Cancel();
        }

        [Fact]
        public async Task GetMessageStream_MultipleMessages_ShouldBeSequential()
        {
            // Arrange
            string groupAddress = "239.0.0.51";
            int port = 5101;
            var options = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                options.LocalIP = "127.0.0.1";
            }

            using var receiver = new MulticastSocketBuilder()
                .WithOptions(options)
                .Build();

            using var sender = new MulticastSocketBuilder()
                .WithOptions(options)
                .Build();

            receiver.StartReceiving();
            
            var stream = receiver.GetMessageStream();

            // Act
            await sender.SendAsync("Msg 1");
            await sender.SendAsync("Msg 2");
            await sender.SendAsync("Msg 3");

            // Assert
            int count = 0;
            await foreach (var msg in stream)
            {
                count++;
                string content = Encoding.UTF8.GetString(msg.Data);
                Assert.Equal($"Msg {count}", content);
                
                if (count == 3) break;
            }
            
            Assert.Equal(3, count);
        }
    }
}
