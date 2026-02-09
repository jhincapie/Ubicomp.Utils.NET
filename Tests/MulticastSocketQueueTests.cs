#nullable enable
using System;
using System.Text;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("SharedTransport")]
    public class MulticastSocketQueueTests
    {
        private const int TestPort = 6000;

        [Fact]
        public void Options_ShouldDefaultTo4096()
        {
            var options = MulticastSocketOptions.LocalNetwork();
            Assert.Equal(4096, options.MaxQueueSize);
        }

        [Fact]
        public void Options_Validate_ShouldThrowIfNonPositive()
        {
            var options = MulticastSocketOptions.LocalNetwork();
            options.MaxQueueSize = 0;
            Assert.Throws<ArgumentException>(() => options.Validate());

            options.MaxQueueSize = -1;
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public async Task Channel_ShouldDropMessages_WhenFull()
        {
            // Setup options with small queue size
            int queueSize = 5;
            int port = TestPort + 1;
            string groupAddress = "239.0.0.99";

            var options = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            options.MaxQueueSize = queueSize;
            // Use loopback to ensure we receive our own messages
            options.MulticastLoopback = true;
            // Filter to loopback interface if possible to reduce noise, or just rely on unique port/group
            string? localIP = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) ? "127.0.0.1" : null;
            options.LocalIP = localIP;

            using var socket = new MulticastSocketBuilder()
                .WithOptions(options)
                .Build();

            // Start receiving
            socket.StartReceiving();

            // Start the channel by consuming one message (or just starting the enumerator)
            // We need to do this because the channel is only active if someone is listening or OnMessageReceived is set.
            // But if we attach OnMessageReceived, it might consume messages?
            // Let's use GetMessageStream.

            var cts = new System.Threading.CancellationTokenSource();
            var enumerator = socket.GetMessageStream(cts.Token).GetAsyncEnumerator();

            // Start the enumeration. This sets _isChannelStarted = true.
            // But MoveNextAsync will block until a message arrives.
            var initTask = enumerator.MoveNextAsync().AsTask();

            // Wait a moment for the task to start and channel to open
            await Task.Delay(100);

            // Send init message
            await socket.SendAsync("Init");

            // Consume the init message
            Assert.True(await initTask);
            Assert.Equal("Init", Encoding.UTF8.GetString(enumerator.Current.Data, 0, enumerator.Current.Length));

            // Now the channel is started and active.
            // Send more messages than queueSize to flood the buffer.
            int messagesToSend = queueSize * 3;
            for (int i = 0; i < messagesToSend; i++)
            {
                await socket.SendAsync($"Msg-{i}");
                // No delay to ensure flooding
            }

            // Give some time for socket to receive and drop
            await Task.Delay(500);

            // Now consume everything remaining in the channel
            int receivedCount = 0;

            // We expect at most queueSize messages to be buffered.
            // The channel is bounded.

            // We need a way to stop reading when channel is empty.
            // Since we can't easily peek, we'll use a timeout approach or just try to read what's there.
            // But GetMessageStream blocks if empty.

            // We can race MoveNextAsync with a timeout.

            while (true)
            {
                var moveNextTask = enumerator.MoveNextAsync().AsTask();
                var timeoutTask = Task.Delay(200); // 200ms timeout

                var completed = await Task.WhenAny(moveNextTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    // Timed out, assume empty
                    break;
                }

                if (await moveNextTask)
                {
                    receivedCount++;
                    // Optional: check content
                    // string content = Encoding.UTF8.GetString(enumerator.Current.Data, 0, enumerator.Current.Length);
                }
                else
                {
                    // End of stream
                    break;
                }
            }

            // Verify
            // We sent `messagesToSend` (15).
            // We expect to receive `queueSize` (5) + maybe 1-2 depending on race conditions or internal buffering?
            // Actually, BoundedChannel with DropWrite drops if full.
            // So we should have exactly `queueSize` items in the channel.
            // However, the socket receive loop might be holding one message in `TryWrite`.
            // So assert receivedCount <= queueSize + 2.

            Assert.True(receivedCount <= queueSize + 2, $"Received {receivedCount} messages, expected roughly {queueSize}. Queue limit validation failed.");

            // Also assert we received *something* (unless all dropped, which is unlikely if we sent slowly enough? No we flooded.)
            // Actually, if we flood extremely fast, we might drop everything if the consumer is completely stopped.
            // But we sent 15 messages. The buffer is 5.
            Assert.True(receivedCount > 0, "Should have buffered some messages.");
        }
    }
}
