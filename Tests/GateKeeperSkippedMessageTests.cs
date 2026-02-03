#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class GateKeeperSkippedMessageTests
    {
        [Fact]
        public void GateKeeper_ShouldNotHangForever_WhenMessageIsSkipped()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            var transport = new TransportComponent(options);
            transport.GateKeeperTimeout = TimeSpan.FromMilliseconds(200);
            
            // We simulate messages arriving out of order or one being skipped.
            // Note: HandleSocketMessage is internal, so we can call it directly.
            
            var msg2Processed = new ManualResetEvent(false);
            int msgType = 101;
            transport.RegisterHandler<string>(msgType, (data, ctx) => {
                if (data == "msg2") msg2Processed.Set();
            });

            // Act
            var source = new EventSource(Guid.NewGuid(), "TestSource");
            var transportMsg = new TransportMessage(source, msgType, "msg2");
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(transportMsg, new Newtonsoft.Json.JsonSerializerSettings {
                Converters = { new TransportMessageConverter() }
            });
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

            // We skip message 1 and send message 2.
            var task = Task.Run(() => {
                transport.HandleSocketMessage(new SocketMessage(data, 2));
            });

            // Assert
            bool received = msg2Processed.WaitOne(2000);
            
            // This is EXPECTED to fail (received == false) with the current implementation.
            // We use this test to confirm the vulnerability.
            Assert.True(received, "GateKeeper hung because message 1 was skipped.");
        }
    }
}
