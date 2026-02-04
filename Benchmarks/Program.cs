using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.Sockets;

namespace Benchmarks
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Benchmark...");

            // Configuration
            const int messageCount = 20000;
            const int port = 5555;
            const string groupAddress = "239.1.1.1";

            // Setup Receiver
            long receivedCount = 0;
            var completedSignal = new TaskCompletionSource<bool>();

            var receiverOptions = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            receiverOptions.MulticastLoopback = true;
            receiverOptions.ReuseAddress = true;
            // Increase buffer size to reduce drops
            receiverOptions.ReceiveBufferSize = 1024 * 1024;

            using var receiver = new MulticastSocketBuilder()
                .WithOptions(receiverOptions)
                .OnMessageReceived((msg) =>
                {
                    long current = Interlocked.Increment(ref receivedCount);
                    if (current >= messageCount)
                    {
                        completedSignal.TrySetResult(true);
                    }
                })
                .Build();

            receiver.StartReceiving();

            // Setup Sender
            var senderOptions = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            senderOptions.MulticastLoopback = true;
            senderOptions.ReuseAddress = true;

            using var sender = new MulticastSocketBuilder()
                .WithOptions(senderOptions)
                .Build();

            // Give time for sockets to join
            await Task.Delay(1000);

            Console.WriteLine($"Sending {messageCount} messages...");

            byte[] payload = new byte[100]; // 100 bytes payload
            new Random().NextBytes(payload);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long initialMemory = GC.GetTotalMemory(true);
            int gen0Start = GC.CollectionCount(0);
            int gen1Start = GC.CollectionCount(1);
            int gen2Start = GC.CollectionCount(2);

            var sw = Stopwatch.StartNew();

            // Send loop
            var sendTask = Task.Run(async () =>
            {
                for (int i = 0; i < messageCount; i++)
                {
                    await sender.SendAsync(payload);
                    // Minimal delay to prevent total buffer saturation immediately
                    if (i % 100 == 0) await Task.Delay(1);
                }
            });

            // Wait for completion
            await Task.WhenAny(completedSignal.Task, Task.Delay(10000)); // 10s timeout

            sw.Stop();

            long finalMemory = GC.GetTotalMemory(false);
            int gen0End = GC.CollectionCount(0);
            int gen1End = GC.CollectionCount(1);
            int gen2End = GC.CollectionCount(2);

            long finalCount = Interlocked.Read(ref receivedCount);

            if (finalCount < messageCount)
            {
                Console.WriteLine($"Timed out! Received {finalCount}/{messageCount}");
            }
            else
            {
                Console.WriteLine("Benchmark Completed.");
            }

            Console.WriteLine($"Time: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"Throughput: {finalCount / sw.Elapsed.TotalSeconds:F2} msg/s");
            Console.WriteLine($"Gen0: {gen0End - gen0Start}");
            Console.WriteLine($"Gen1: {gen1End - gen1Start}");
            Console.WriteLine($"Gen2: {gen2End - gen2Start}");
            Console.WriteLine($"Memory Diff: {(finalMemory - initialMemory) / 1024.0:F2} KB");

            receiver.Close();
            sender.Close();
        }
    }
}
