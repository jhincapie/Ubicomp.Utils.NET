#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class PeerDiscoveryTests
    {
        [Fact]
        public Task Nodes_ShouldDiscoverEachOther()
        {
            // Arrange
            string groupAddress = "239.0.0.60";
            int port = 5102;
            var options = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            TestConfiguration.ConfigureOptions(options);

            var peer1Discovered = new ManualResetEvent(false);
            var peer2Discovered = new ManualResetEvent(false);

            using var node1 = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithLocalSource("Node1")
                .WithHeartbeat(TimeSpan.FromMilliseconds(50))
                .Build();

            using var node2 = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithLocalSource("Node2")
                .WithHeartbeat(TimeSpan.FromMilliseconds(50))
                .Build();

            node1.OnPeerDiscovered += (p) =>
            {
                if (p.DeviceName == "Node2")
                    peer1Discovered.Set();
            };

            node2.OnPeerDiscovered += (p) =>
            {
                if (p.DeviceName == "Node1")
                    peer2Discovered.Set();
            };

            // Act
            node1.Start();
            node2.Start();

            // Assert
            bool success = WaitHandle.WaitAll(new WaitHandle[] { peer1Discovered, peer2Discovered }, 2000);
            Assert.True(success, "Nodes failed to discover each other within timeout.");

            var peers1 = node1.ActivePeers.ToList();
            var peers2 = node2.ActivePeers.ToList();

            Assert.Single(peers1);
            Assert.Single(peers2);
            Assert.Equal("Node2", peers1[0].DeviceName);
            Assert.Equal("Node1", peers2[0].DeviceName);

            return Task.CompletedTask;
        }

        [Fact]
        public Task Metadata_ShouldBePropagated()
        {
            // Arrange
            string groupAddress = "239.0.0.61";
            int port = 5103;
            var options = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            TestConfiguration.ConfigureOptions(options);

            var discoveryEvent = new ManualResetEvent(false);
            RemotePeer? receivedPeer = null;

            using var receiver = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithLocalSource("Receiver")
                .WithHeartbeat(TimeSpan.FromMilliseconds(50))
                .Build();

            using var sender = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithLocalSource("Sender")
                .WithHeartbeat(TimeSpan.FromMilliseconds(50))
                .WithInstanceMetadata("{\"Role\":\"Master\"}")
                .Build();

            receiver.OnPeerDiscovered += (p) =>
            {
                if (p.DeviceName == "Sender")
                {
                    receivedPeer = p;
                    discoveryEvent.Set();
                }
            };

            // Act
            receiver.Start();
            sender.Start();

            // Assert
            Assert.True(discoveryEvent.WaitOne(2000));
            Assert.NotNull(receivedPeer);
            Assert.Equal("{\"Role\":\"Master\"}", receivedPeer!.Metadata);

            return Task.CompletedTask;
        }

        [Fact]
        public Task StalePeers_ShouldBeRemoved()
        {
            // Arrange
            string groupAddress = "239.0.0.62";
            int port = 5104;
            var options = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            TestConfiguration.ConfigureOptions(options);

            var discoveryEvent = new ManualResetEvent(false);
            var lostEvent = new ManualResetEvent(false);

            // Receiver expects frequent heartbeats (50ms interval, 150ms timeout)
            using var receiver = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithLocalSource("Receiver")
                .WithHeartbeat(TimeSpan.FromMilliseconds(50))
                .Build();

            // Sender
            using var sender = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithSocket(TestConfiguration.CreateSocket(options))
                .WithLocalSource("Sender")
                .WithHeartbeat(TimeSpan.FromMilliseconds(50))
                .Build();

            receiver.OnPeerDiscovered += (p) => discoveryEvent.Set();
            receiver.OnPeerLost += (p) => lostEvent.Set();

            receiver.Start();
            sender.Start();

            // Wait for discovery
            Assert.True(discoveryEvent.WaitOne(1000), "Failed to discover peer initially.");

            // Stop sender to simulate failure
            sender.Stop();

            // Act - wait for timeout
            // Timeout is 3 * 50ms = 150ms. Wait slightly longer.
            // HeartbeatLoop runs every 50ms and checks.
            bool lost = lostEvent.WaitOne(1000);

            // Assert
            Assert.True(lost, "Peer was not removed after stopping heartbeats.");
            Assert.Empty(receiver.ActivePeers);

            return Task.CompletedTask;
        }
    }
}
