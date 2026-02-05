using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class IntegrityTests
    {
        [Fact]
        public async Task Test_Integrity_DefaultSHA256_Success()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork("239.1.2.3", 6001);

            var tcs = new TaskCompletionSource<string>();

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithLogging(NullLoggerFactory.Instance)
                .RegisterHandler<SimpleMessage>((msg, ctx) =>
                {
                    tcs.TrySetResult(msg.Content);
                })
                .Build();

            // Act
            transport.Start();
            await transport.SendAsync(new SimpleMessage { Content = "Hello SHA256" });

            var result = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            transport.Stop();

            // Assert
            Assert.Equal(tcs.Task, result); // Message should be received
            var content = await tcs.Task;
            Assert.Equal("Hello SHA256", content);
            // Verify SecurityKey is null
            Assert.Null(transport.SecurityKey);
        }

        [Fact]
        public async Task Test_Integrity_WithHMACKey_Success()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork("239.1.2.3", 6002);

            var key = Convert.ToBase64String(new byte[32]); // Dummy 32-byte key

            var tcs = new TaskCompletionSource<string>();

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithSecurityKey(key)
                .WithLogging(NullLoggerFactory.Instance)
                .RegisterHandler<SimpleMessage>((msg, ctx) =>
                {
                    tcs.TrySetResult(msg.Content);
                })
                .Build();

            // Act
            transport.Start();
            await transport.SendAsync(new SimpleMessage { Content = "Hello HMAC" });

            var result = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            transport.Stop();

            // Assert
            Assert.Equal(tcs.Task, result); // Message should be received
            var content = await tcs.Task;
            Assert.Equal("Hello HMAC", content);
            Assert.Equal(key, transport.SecurityKey);
        }
    }

    [MessageType("test.simple")]
    public class SimpleMessage
    {
        public string Content { get; set; } = "";
    }
}
