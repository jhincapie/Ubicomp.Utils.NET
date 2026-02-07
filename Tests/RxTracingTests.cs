using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class RxTracingTests
    {
        [Fact]
        public async Task RxStream_ShouldReceiveMessages()
        {
            // Arrange
            string groupAddress = "239.0.0.70";
            int port = 5105;
            var options = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            TestConfiguration.ConfigureOptions(options);

            using var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithLocalSource("RxNode")
                .Build();

            var messageReceived = new ManualResetEvent(false);
            int count = 0;

            // Subscribe to Rx stream
            using var subscription = transport.MessageStream.Subscribe(msg =>
            {
                count++;
                messageReceived.Set();
            });

            transport.Start();

            // Act
            await transport.SendAsync(new HeartbeatMessage { DeviceName = "Test" });

            // Assert
            Assert.True(messageReceived.WaitOne(1000));
            Assert.True(count > 0);
        }

        [Fact]
        public async Task Activities_ShouldBeCreated()
        {
            // Arrange
            string groupAddress = "239.0.0.71";
            int port = 5106;
            var options = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            TestConfiguration.ConfigureOptions(options);

            using var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithLocalSource("TraceNode")
                .Build();

            bool activityStarted = false;
            object lockObj = new object();

            // Listen for activities
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Ubicomp.Utils.NET.Transport",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity =>
                {
                    lock (lockObj)
                    {
                        if (activity.OperationName == "SendMessage" || activity.OperationName == "ReceiveMessage")
                        {
                            activityStarted = true;
                        }
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);

            transport.Start();

            // Act
            await transport.SendAsync(new HeartbeatMessage { DeviceName = "TraceTest" });

            // Wait allow loopback
            await Task.Delay(500);

            // Assert
            Assert.True(activityStarted, "No activity was started for Send/Receive.");
        }
    }
}
