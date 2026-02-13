using System;
using System.Buffers;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.MulticastTransportFramework.Components;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ubicomp.Utils.NET.Tests
{
    public class FastReplayProtectionTests
    {
        [Fact]
        public void IsReplay_ReturnsTrueForDuplicates()
        {
            var window = new ReplayWindow();
            // Mark sequence 10 as seen
            Assert.True(window.CheckAndMark(10));

            // CheckIsReplay should say true for 10
            Assert.True(window.IsReplay(10));

            // CheckIsReplay should say false for 11 (new)
            Assert.False(window.IsReplay(11));
        }

        [Fact]
        public void IsReplay_ReturnsTrueForOldPackets()
        {
            var window = new ReplayWindow();
            Assert.True(window.CheckAndMark(100)); // Highest = 100

            // Window size 64. 100-64 = 36 is boundary.
            // 30 is too old.
            Assert.True(window.IsReplay(30));
        }

        [Fact]
        public void IsReplay_ReturnsFalseForNewPackets()
        {
            var window = new ReplayWindow();
            Assert.False(window.IsReplay(1));
            // Should verify that IsReplay does NOT change state
            Assert.False(window.IsReplay(1));

            // Now mark it
            Assert.True(window.CheckAndMark(1));
            // Now it is replay
            Assert.True(window.IsReplay(1));
        }

        [Fact]
        public void ReplayProtector_IsKnownReplay_DelegatesCorrectly()
        {
            var protector = new ReplayProtector(NullLogger.Instance);
            var sourceId = Guid.NewGuid();

            // Simulate a valid message passing through normal flow (creating window)
            // But we can't easily inject state into ReplayProtector without using internal knowledge or reflection,
            // OR simply by passing a dummy message to IsValid first.

            var msg = new TransportMessage
            {
                MessageId = Guid.NewGuid(),
                MessageSource = new EventSource(sourceId, "Test"),
                SenderSequenceNumber = 50,
                Ticks = DateTime.UtcNow.Ticks,
                MessageType = "test",
                MessageData = "payload"
            };

            Assert.True(protector.IsValid(msg, out _)); // Valid, marks 50

            // Now check IsKnownReplay for 50
            Assert.True(protector.IsKnownReplay(sourceId, 50, DateTime.UtcNow.Ticks, out var reason));
            Assert.Contains("Duplicate", reason);

            // Check 51 (new)
            Assert.False(protector.IsKnownReplay(sourceId, 51, DateTime.UtcNow.Ticks, out _));
        }

        [Fact]
        public void ReplayProtector_IsKnownReplay_HandlesUnknownSource()
        {
            var protector = new ReplayProtector(NullLogger.Instance);
            var sourceId = Guid.NewGuid();

            // Unknown source -> Not a replay
            Assert.False(protector.IsKnownReplay(sourceId, 1, DateTime.UtcNow.Ticks, out _));
        }

        [Fact]
        public void ReplayProtector_IsKnownReplay_HandlesOldTimestamp()
        {
            var protector = new ReplayProtector(NullLogger.Instance);
            var sourceId = Guid.NewGuid();
            var oldTicks = DateTime.UtcNow.AddMinutes(-10).Ticks;

            // Even if source is unknown, timestamp check happens first
            Assert.True(protector.IsKnownReplay(sourceId, 1, oldTicks, out var reason));
            Assert.Contains("Message too old", reason);
        }

        [Fact]
        public void BinaryPacket_TryReadHeader_ExtractsSourceId()
        {
            // Create a valid packet manually or via SerializeToWriter
            var msg = new TransportMessage
            {
                MessageId = Guid.NewGuid(),
                MessageSource = new EventSource(Guid.NewGuid(), "Test"),
                SenderSequenceNumber = 123,
                Ticks = DateTime.UtcNow.Ticks,
                MessageType = "test",
                MessageData = "payload"
            };

            var writer = new Ubicomp.Utils.NET.MulticastTransportFramework.ArrayBufferWriter<byte>();
            BinaryPacket.SerializeToWriter(writer, msg, null, null);

            var buffer = writer.WrittenSpan;

            Assert.True(BinaryPacket.TryReadHeader(buffer, out var header));
            Assert.Equal(msg.MessageSource.ResourceId, header.SourceId);
            Assert.Equal(msg.SenderSequenceNumber, header.SenderSequenceNumber);
            Assert.Equal(msg.Ticks, header.Ticks);
        }
    }
}
