using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.MulticastTransportFramework.Components;
using Xunit;

namespace Ubicomp.Utils.NET.Tests.Components
{
    public class AckManagerTests
    {
        [Fact]
        public async Task CreateSession_TracksAndCompletes()
        {
            var manager = new AckManager(NullLogger.Instance);
            var id = Guid.NewGuid();
            var session = manager.CreateSession(id, TimeSpan.FromSeconds(1));

            Assert.False(session.IsAnyAckReceived);

            // Simulate incoming ack
            manager.ProcessIncomingAck(id, new EventSource(Guid.NewGuid(), "Responder"));

            Assert.True(session.IsAnyAckReceived);
        }

        [Fact]
        public async Task CreateSession_CleansUpAfterTimeout()
        {
            var manager = new AckManager(NullLogger.Instance);
            var id = Guid.NewGuid();
            var session = manager.CreateSession(id, TimeSpan.FromMilliseconds(50));

            // Wait for timeout (generous margin)
            await Task.Delay(500);

            // Verify removal by checking that processing an ack doesn't update the stale session
            manager.ProcessIncomingAck(id, new EventSource(Guid.NewGuid(), "Responder"));

            Assert.False(session.IsAnyAckReceived);
        }

        [Fact]
        public void ShouldAutoSendAck_Logic()
        {
            var manager = new AckManager(NullLogger.Instance) { AutoSendAcks = true };
            var protector = new ReplayProtector(NullLogger.Instance);
            var source = new EventSource(Guid.NewGuid(), "Test");

            // Ensure protector knows about source
            protector.IsValid(new TransportMessage(source, "Init", "Data") { SenderSequenceNumber = 1 }, out _);

            // 1. Valid Request
            var msg = new TransportMessage(source, "TestType", "Data") { RequestAck = true };
            Assert.True(manager.ShouldAutoSendAck(msg, protector)); // Consumes 1 token (9 left)

            // 2. AutoSendAcks = false
            manager.AutoSendAcks = false;
            Assert.False(manager.ShouldAutoSendAck(msg, protector));
            manager.AutoSendAcks = true;

            // 3. RequestAck = false
            msg.RequestAck = false;
            Assert.False(manager.ShouldAutoSendAck(msg, protector));
            msg.RequestAck = true;

            // 4. MessageType = Ack
            msg.MessageType = TransportComponent.AckMessageType;
            Assert.False(manager.ShouldAutoSendAck(msg, protector));
            msg.MessageType = "TestType";

            // 5. Rate Limit Exceeded
            // We have 9 tokens left. Consume them.
            for (int i = 0; i < 9; i++)
            {
                Assert.True(manager.ShouldAutoSendAck(msg, protector));
            }
            // Now 0 tokens. Next should fail.
            Assert.False(manager.ShouldAutoSendAck(msg, protector));
        }
    }
}
