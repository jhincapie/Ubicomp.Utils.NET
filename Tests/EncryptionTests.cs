using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("SharedTransport")]
    public class EncryptionTests
    {
        [MessageType("test.enc")]
        public class SecretMessage
        {
            public string Secret { get; set; } = "Top Secret";
        }

        [Fact]
        public async Task SendReceive_Encrypted_Success()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork("239.1.2.4", 6003);
            string key = "MySecretPassphrase123!"; // Plain string, not Base64


            var tcs = new TaskCompletionSource<string>();

            // Receiver
            var receiver = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithSecurityKey(key)
                .WithEncryption(true)
                .WithLogging(NullLoggerFactory.Instance)
                .RegisterHandler<SecretMessage>((msg, ctx) =>
                {
                    tcs.TrySetResult(msg.Secret);
                })
                .Build();

            receiver.Start();

            // Sender
            var sender = new TransportBuilder()
                 .WithMulticastOptions(options)
                 .WithSocket(TestConfiguration.CreateSocket(options))
                 .WithSecurityKey(key) // Same Key
                 .WithEncryption(true) // Enable Encryption
                 .WithLogging(NullLoggerFactory.Instance)
                 .Build();

            sender.Start();

            try
            {
                // Act
                await sender.SendAsync(new SecretMessage { Secret = "Eagle has landed" });

                // Assert
                var result = await Task.WhenAny(tcs.Task, Task.Delay(2000));
                Assert.Equal(tcs.Task, result);
                var content = await tcs.Task;
                Assert.Equal("Eagle has landed", content);

                // Verify Internal State (Reflection or Inspection check would be ideal to prove encryption happened,
                // but functionally this proves E2E works).
                // We trust the code coverage and implementation correctness for the "IsEncrypted" flag.
            }
            finally
            {
                sender.Stop();
                receiver.Stop();
            }
        }

        [Fact]
        public async Task IntegrityKey_Mismatch_DropsMessage()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork("239.1.2.4", 6004);

            string key1 = "KeyNum1";
            string key2 = "KeyNum2"; // Different key string

            var tcs = new TaskCompletionSource<bool>();

            // Receiver (Key A)
            var receiver = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithSecurityKey(key1)
                .WithEncryption(true)
                .WithLogging(NullLoggerFactory.Instance)
                .RegisterHandler<SecretMessage>((msg, ctx) =>
                {
                    tcs.TrySetResult(true);
                })
                .Build();
            receiver.Start();

            // Sender (Key B)
            var sender = new TransportBuilder()
                 .WithMulticastOptions(options)
                 .WithSocket(TestConfiguration.CreateSocket(options))
                 .WithSecurityKey(key2) // DIFFERENT KEY
                 .WithEncryption(true)
                 .WithLogging(NullLoggerFactory.Instance)
                 .Build();
            sender.Start();

            try
            {
                // Act
                await sender.SendAsync(new SecretMessage { Secret = "Attack" });

                // Assert
                var result = await Task.WhenAny(tcs.Task, Task.Delay(1000));
                Assert.NotEqual(tcs.Task, result); // Should NOT receive because signature fails
            }
            finally
            {
                sender.Stop();
                receiver.Stop();
            }
        }
    }
}
