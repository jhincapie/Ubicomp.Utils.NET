using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.Sockets;
using Xunit;
using Xunit.Abstractions;

namespace Ubicomp.Utils.NET.Tests
{
    public class PerformanceTests
    {
        private readonly ITestOutputHelper _output;

        public PerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task MeasureReceiveAllocation()
        {
            // Use a random port to avoid conflicts
            int port = 5500 + new Random().Next(100, 1000);
            string group = "239.0.0.55";
            var options = MulticastSocketOptions.LocalNetwork(group, port);
            options.MulticastLoopback = true;
            options.ReuseAddress = true;

            int messageCount = 1000;
            int receivedCount = 0;
            var tcs = new TaskCompletionSource<bool>();

            // Sender and Receiver
            using var sender = new MulticastSocketBuilder().WithOptions(options).Build();
            using var receiver = new MulticastSocketBuilder()
                .WithOptions(options)
                .OnMessageReceived((msg) =>
                {
                    receivedCount++;
                    if (receivedCount >= messageCount)
                    {
                        tcs.TrySetResult(true);
                    }
                    var len = msg.Data.Length;
                    msg.Dispose();
                })
                .Build();

            receiver.StartReceiving();

            // Warmup
            await sender.SendAsync(new byte[100]);
            await Task.Delay(200);
            receivedCount = 0;
            tcs = new TaskCompletionSource<bool>();

            // Force GC to get a clean slate
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long initialAllocated = GC.GetTotalAllocatedBytes(true);

            byte[] payload = new byte[256];
            new Random().NextBytes(payload);

            for (int i = 0; i < messageCount; i++)
            {
                await sender.SendAsync(payload);
                if (i % 100 == 0) await Task.Delay(1); // Yield slightly
            }

            // Wait for receive
            var task = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            if (task != tcs.Task)
            {
                _output.WriteLine($"Timed out! Received {receivedCount}/{messageCount}");
            }

            long finalAllocated = GC.GetTotalAllocatedBytes(true);
            long totalAllocated = finalAllocated - initialAllocated;

            _output.WriteLine($"Allocated: {totalAllocated:N0} bytes for {receivedCount} messages.");
            if (receivedCount > 0)
                _output.WriteLine($"Bytes per message: {totalAllocated / (double)receivedCount:N2}");
        }
    }
}
