using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;
using System.Text.Json;
using System.Text;

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("TransportBuilderTests")]
    public class OrderingConfigurationTests
    {
        [Fact]
        public void Builder_ShouldApplyEnforceOrdering_WhenConfigured()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();

            // Act
            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithEnforceOrdering(true)
                .Build();

            // Assert
            Assert.True(transport.EnforceOrdering, "EnforceOrdering should be true after builder configuration.");

            // Act again (override to false)
            transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithEnforceOrdering(false)
                .Build();

            Assert.False(transport.EnforceOrdering, "EnforceOrdering should be false after builder configuration.");
        }

        [Fact]
        public async Task Transport_ShouldEnforceOrdering_WhenConfiguredTrue()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithEnforceOrdering(true)
                .Build();

            try { transport.Start(); } catch { }

            var processedMessages = new List<string>();
            var completionSource = new TaskCompletionSource<bool>();

            transport.RegisterHandler<string>("test.ordering", (data, ctx) =>
            {
                lock (processedMessages)
                {
                    processedMessages.Add(data);
                    if (processedMessages.Count == 2)
                    {
                        completionSource.TrySetResult(true);
                    }
                }
            });

            // Act
            // Simulate receiving Message 2 first
            var source = new EventSource(Guid.NewGuid(), "TestSource");

            // Create msg 2
            byte[] data2 = CreateSocketMessageData(source, "test.ordering", "msg2", transport);
            // Create msg 1
            byte[] data1 = CreateSocketMessageData(source, "test.ordering", "msg1", transport);

            // Inject msg 2 (Sequence 2) - should be buffered because we expect 1
            transport.HandleSocketMessage(new SocketMessage(data2, 2));

            // Check it hasn't been processed yet
            lock(processedMessages)
            {
                Assert.Empty(processedMessages);
            }

            // Inject msg 1 (Sequence 1)
            transport.HandleSocketMessage(new SocketMessage(data1, 1));

            // Assert
            // Should process msg 1, then msg 2
            var timeoutTask = Task.Delay(2000);
            var completedTask = await Task.WhenAny(completionSource.Task, timeoutTask);
            Assert.True(completedTask == completionSource.Task, "Should have received both messages before timeout.");
            Assert.True(await completionSource.Task, "Completion source should be true.");

            lock (processedMessages)
            {
                Assert.Equal(2, processedMessages.Count);
                Assert.Equal("msg1", processedMessages[0]);
                Assert.Equal("msg2", processedMessages[1]);
            }
            transport.Stop();
        }

        [Fact]
        public void Transport_ShouldProcessImmediately_WhenConfiguredFalse()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithEnforceOrdering(false)
                .Build();

            try { transport.Start(); } catch { }

            var processedMessages = new List<string>();
            var msgEvent = new AutoResetEvent(false);

            transport.RegisterHandler<string>("test.ordering", (data, ctx) =>
            {
                lock (processedMessages)
                {
                    processedMessages.Add(data);
                }
                msgEvent.Set();
            });

            // Act
            var source = new EventSource(Guid.NewGuid(), "TestSource");
            byte[] data2 = CreateSocketMessageData(source, "test.ordering", "msg2", transport);

            // Inject msg 2 (Sequence 2) - should be processed immediately
            transport.HandleSocketMessage(new SocketMessage(data2, 2));

            // Assert
            bool received = msgEvent.WaitOne(500);
            transport.Stop();
            Assert.True(received, "Message 2 should be processed immediately.");

            lock (processedMessages)
            {
                Assert.Single(processedMessages);
                Assert.Equal("msg2", processedMessages[0]);
            }
        }

        private byte[] CreateSocketMessageData<T>(EventSource source, string msgType, T content, TransportComponent transport)
        {
            var transportMsg = new TransportMessage(source, msgType, content);

            // Sign the message manually
            transportMsg.Signature = transport.ComputeSignature(transportMsg, null);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            string json = JsonSerializer.Serialize(transportMsg, options);
            return Encoding.UTF8.GetBytes(json);
        }
    }
}
