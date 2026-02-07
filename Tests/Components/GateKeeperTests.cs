using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.MulticastTransportFramework.Components;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests.Components
{
    public class GateKeeperTests
    {
        [Fact]
        public async Task OrderedMessages_PassThrough()
        {
            var gk = new GateKeeper(NullLogger.Instance);
            var output = Channel.CreateUnbounded<SocketMessage>();
            gk.Start(output.Writer);

            var msg1 = new SocketMessage(new byte[0], 1);
            gk.TryPush(msg1);

            var received = await output.Reader.ReadAsync();
            Assert.Equal(1, received.ArrivalSequenceId);

            gk.Stop();
        }

        [Fact]
        public async Task OutOfOrder_Buffered()
        {
            var gk = new GateKeeper(NullLogger.Instance);
            var output = Channel.CreateUnbounded<SocketMessage>();
            gk.Start(output.Writer);

            // Send 3 (ArrivalSequenceId = 3), skipping 1 and 2
            // Wait, GateKeeper assumes start at 1.
            var msg3 = new SocketMessage(new byte[0], 3);
            gk.TryPush(msg3);

            // Should not receive anything yet
            var task = output.Reader.WaitToReadAsync().AsTask();
            var completed = await Task.WhenAny(task, Task.Delay(100));
            Assert.NotEqual(task, completed); // Should timeout

            // Send 1
            var msg1 = new SocketMessage(new byte[0], 1);
            gk.TryPush(msg1);

            var r1 = await output.Reader.ReadAsync();
            Assert.Equal(1, r1.ArrivalSequenceId);

            // Still waiting for 2. 3 is buffered.

            // Send 2
            var msg2 = new SocketMessage(new byte[0], 2);
            gk.TryPush(msg2);

            var r2 = await output.Reader.ReadAsync();
            Assert.Equal(2, r2.ArrivalSequenceId);

            var r3 = await output.Reader.ReadAsync();
            Assert.Equal(3, r3.ArrivalSequenceId);

            gk.Stop();
        }

        [Fact]
        public async Task GapTimeout_SkipsMissing()
        {
            var gk = new GateKeeper(NullLogger.Instance) { GateKeeperTimeout = TimeSpan.FromMilliseconds(200) };
            var output = Channel.CreateUnbounded<SocketMessage>();
            gk.Start(output.Writer);

            var msg1 = new SocketMessage(new byte[0], 1);
            gk.TryPush(msg1);
            await output.Reader.ReadAsync();

            // Send 3 (skip 2)
            var msg3 = new SocketMessage(new byte[0], 3);
            gk.TryPush(msg3);

            // Wait for timeout
            var r3 = await output.Reader.ReadAsync(); // This should block until timeout fires
            Assert.Equal(3, r3.ArrivalSequenceId);

            gk.Stop();
        }
    }
}
