using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.MulticastTransportFramework.Components;
using Xunit;

namespace Ubicomp.Utils.NET.Tests.Components
{
    public class PeerManagerTests
    {
        [Fact]
        public async Task Start_SendsHeartbeats()
        {
            var manager = new PeerManager(NullLogger.Instance);
            var localSource = new EventSource(Guid.NewGuid(), "Local");
            manager.HeartbeatInterval = TimeSpan.FromMilliseconds(50);

            int sentCount = 0;
            var tcs = new TaskCompletionSource<bool>();

            manager.Start(async msg =>
            {
                Interlocked.Increment(ref sentCount);
                if (sentCount >= 2) tcs.TrySetResult(true);
                await Task.CompletedTask;
            }, localSource.ResourceId.ToString(), localSource.ResourceName);

            var task = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.True(task == tcs.Task, "Timed out waiting for heartbeats");

            manager.Stop();
        }

        [Fact]
        public void HandleHeartbeat_UpdatesPeers()
        {
            var manager = new PeerManager(NullLogger.Instance);
            var localId = Guid.NewGuid();
            var localSource = new EventSource(localId, "Local");

            bool discovered = false;
            manager.OnPeerDiscovered += (p) => discovered = true;

            var remoteId = Guid.NewGuid().ToString();
            manager.HandleHeartbeat(new HeartbeatMessage
            {
                SourceId = remoteId,
                DeviceName = "Remote"
            }, localId.ToString());

            Assert.True(discovered);
            Assert.Contains(manager.ActivePeers, p => p.SourceId == remoteId);
        }

        [Fact]
        public void HandleHeartbeat_IgnoresSelf()
        {
            var id = Guid.NewGuid();
            var manager = new PeerManager(NullLogger.Instance);
            var localSource = new EventSource(id, "Local");

            bool discovered = false;
            manager.OnPeerDiscovered += (p) => discovered = true;

            manager.HandleHeartbeat(new HeartbeatMessage
            {
                SourceId = id.ToString(),
                DeviceName = "Local"
            }, id.ToString());

            Assert.False(discovered);
            Assert.Empty(manager.ActivePeers);
        }
    }
}
