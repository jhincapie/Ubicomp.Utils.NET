using System;
using System.Text;
using System.Threading;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("SharedTransport")]
    public class IntegrityTests
    {
        [MessageType("test.integrity")]
        private class IntegrityContent
        {
            public string Data { get; set; } = "";
        }

        [Fact]
        public void Receive_SignedMessage_WhenKeyIsSet_AcceptsMessage()
        {
            // Arrange
            // Use unique ports
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5010);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                options.LocalIP = "127.0.0.1";
            }
            string key = "super_secret_key";

            var receivedEvent = new ManualResetEventSlim(false);

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSecurityKey(key)
                .RegisterHandler<IntegrityContent>((content, context) =>
                {
                    receivedEvent.Set();
                })
                .Build();

            transport.Start();

            try
            {
                // Act
                // We send using the same transport (loopback) which will sign it
                var content = new IntegrityContent { Data = "Valid" };
                transport.SendAsync(content).Wait();

                // Assert
                bool received = receivedEvent.Wait(2000);
                Assert.True(received, "Signed message should be accepted");
            }
            finally
            {
                transport.Stop();
            }
        }

        [Fact]
        public void Receive_UnsignedMessage_WhenKeyIsSet_DropsMessage()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5011);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                options.LocalIP = "127.0.0.1";
            }
            string key = "super_secret_key";

            var receivedEvent = new ManualResetEventSlim(false);

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSecurityKey(key)
                .RegisterHandler<IntegrityContent>((content, context) =>
                {
                    receivedEvent.Set();
                })
                .Build();

            transport.Start();

            try
            {
                // Manually construct an unsigned message
                var messageId = Guid.NewGuid();
                var timeStamp = DateTime.UtcNow.ToString(TransportMessage.DATE_FORMAT_NOW);
                var json = $@"{{
                    ""messageId"": ""{messageId}"",
                    ""messageType"": ""test.integrity"",
                    ""messageSource"": {{ ""resourceId"": ""{Guid.NewGuid()}"", ""resourceName"": ""attacker"" }},
                    ""timeStamp"": ""{timeStamp}"",
                    ""messageData"": {{ ""data"": ""Valid"" }}
                }}";
                // Note: No 'hash' field

                var bytes = Encoding.UTF8.GetBytes(json);
                var socketMsg = new SocketMessage(bytes, 1);

                // Act
                transport.HandleSocketMessage(socketMsg);

                // Assert
                bool received = receivedEvent.Wait(2000);
                Assert.False(received, "Unsigned message should be dropped");
            }
            finally
            {
                transport.Stop();
            }
        }

        [Fact]
        public void Receive_InvalidSignature_WhenKeyIsSet_DropsMessage()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5012);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                options.LocalIP = "127.0.0.1";
            }
            string key = "super_secret_key";

            var receivedEvent = new ManualResetEventSlim(false);

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSecurityKey(key)
                .RegisterHandler<IntegrityContent>((content, context) =>
                {
                    receivedEvent.Set();
                })
                .Build();

            transport.Start();

            try
            {
                // Manually construct message with fake hash
                var messageId = Guid.NewGuid();
                var timeStamp = DateTime.UtcNow.ToString(TransportMessage.DATE_FORMAT_NOW);
                var json = $@"{{
                    ""messageId"": ""{messageId}"",
                    ""messageType"": ""test.integrity"",
                    ""messageSource"": {{ ""resourceId"": ""{Guid.NewGuid()}"", ""resourceName"": ""attacker"" }},
                    ""timeStamp"": ""{timeStamp}"",
                    ""hash"": ""ZmFrZWhhc2g="",
                    ""messageData"": {{ ""data"": ""Valid"" }}
                }}";

                var bytes = Encoding.UTF8.GetBytes(json);
                var socketMsg = new SocketMessage(bytes, 1);

                // Act
                transport.HandleSocketMessage(socketMsg);

                // Assert
                bool received = receivedEvent.Wait(2000);
                Assert.False(received, "Invalid signature message should be dropped");
            }
            finally
            {
                transport.Stop();
            }
        }
    }
}
