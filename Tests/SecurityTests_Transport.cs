using System;
using System.Text;
using System.Threading;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("SharedTransport")]
    public class SecurityTests_Transport
    {
        [MessageType("test.security")]
        private class SecurityContent
        {
            public string Data { get; set; } = "";
        }

        [Fact]
        public void Receive_InvalidTimestamp_ReplacesWithCurrentTime()
        {
            // Arrange
            // Use a different port to avoid conflicts with other tests
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5001);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                options.LocalIP = "127.0.0.1";
            }

            string? receivedTimestamp = null;
            var receivedEvent = new ManualResetEventSlim(false);

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .RegisterHandler<SecurityContent>((content, context) =>
                {
                    receivedTimestamp = context.Timestamp;
                    receivedEvent.Set();
                })
                .Build();

            // We need to Start() to initialize channels
            transport.Start();

            try
            {
                // Construct malicious JSON with ESCAPED newline so it is valid JSON
                // "2025-01-01\n<script>" in JSON string becomes 2025-01-01<LF><script> in memory
                var invalidTimestamp = @"2025-01-01\n<script>";
                var messageId = Guid.NewGuid();
                // Minimal JSON structure matching TransportMessage
                var json = $@"{{
                    ""messageId"": ""{messageId}"",
                    ""messageType"": ""test.security"",
                    ""messageSource"": {{ ""resourceId"": ""{Guid.NewGuid()}"", ""resourceName"": ""attacker"" }},
                    ""timeStamp"": ""{invalidTimestamp}"",
                    ""messageData"": {{ ""data"": ""payload"" }}
                }}";

                var bytes = Encoding.UTF8.GetBytes(json);
                var socketMsg = new SocketMessage(bytes, 1);

                // Act
                // Inject message directly using internal method
                transport.HandleSocketMessage(socketMsg);

                // Assert
                bool received = receivedEvent.Wait(2000);
                Assert.True(received, "Message was not processed");

                Assert.NotNull(receivedTimestamp);
                // The received timestamp should NOT contain the invalid content
                Assert.DoesNotContain("<script>", receivedTimestamp);
                Assert.True(DateTime.TryParse(receivedTimestamp, out _), $"Timestamp should be valid, but was {receivedTimestamp}");
            }
            finally
            {
                transport.Stop();
            }
        }
    }
}
