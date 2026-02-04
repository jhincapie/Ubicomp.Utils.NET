using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;
using Newtonsoft.Json;
using System.Text;

namespace Ubicomp.Utils.NET.Tests
{
    public class OrderingConfigurationTests
    {
        [Fact]
        public void Builder_ShouldApplyEnforceOrdering_WhenConfigured()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            options.EnforceOrdering = false; // Default

            // Act
            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithEnforceOrdering(true)
                .Build();

            // Assert
            Assert.True(options.EnforceOrdering, "EnforceOrdering should be true after builder configuration.");

            // Act again (override to false)
            transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithEnforceOrdering(false)
                .Build();

            Assert.False(options.EnforceOrdering, "EnforceOrdering should be false after builder configuration.");
        }

        [Fact]
        public async Task Transport_ShouldEnforceOrdering_WhenConfiguredTrue()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            // Ensure options start as false so we rely on builder to set it
            options.EnforceOrdering = false;

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithEnforceOrdering(true)
                .Build();

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
            byte[] data2 = CreateSocketMessageData(source, "test.ordering", "msg2");
            // Create msg 1
            byte[] data1 = CreateSocketMessageData(source, "test.ordering", "msg1");

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
        }

        [Fact]
        public void Transport_ShouldProcessImmediately_WhenConfiguredFalse()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            options.EnforceOrdering = true; // Set to true initially to ensure builder overrides it

            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithEnforceOrdering(false)
                .Build();

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
            byte[] data2 = CreateSocketMessageData(source, "test.ordering", "msg2");

            // Inject msg 2 (Sequence 2) - should be processed immediately
            transport.HandleSocketMessage(new SocketMessage(data2, 2));

            // Assert
            bool received = msgEvent.WaitOne(500);
            Assert.True(received, "Message 2 should be processed immediately.");

            lock (processedMessages)
            {
                Assert.Single(processedMessages);
                Assert.Equal("msg2", processedMessages[0]);
            }
        }

        private byte[] CreateSocketMessageData<T>(EventSource source, string msgType, T content)
        {
            var transportMsg = new TransportMessage(source, msgType, content);
            string json = JsonConvert.SerializeObject(transportMsg, new JsonSerializerSettings
            {
                Converters = { new TransportMessageConverter(new Dictionary<string, Type>()) }
            });
            return Encoding.UTF8.GetBytes(json);
        }
    }
}
