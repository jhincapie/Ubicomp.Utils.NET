using System;
using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.MulticastTransportFramework.Components;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests.Components
{
    public class MessageSerializerTests
    {
        [Fact]
        public void SerializeDeserialize_RoundTrip_Binary()
        {
            var security = new SecurityHandler(NullLogger.Instance);
            var serializer = new MessageSerializer(NullLogger.Instance);

            // Use an object to ensure valid JSON serialization, or a valid JSON string
            var content = new TestData { Value = "Data" };
            var msg = new TransportMessage(new EventSource(Guid.NewGuid(), "Src"), "Type", content) { SenderSequenceNumber = 10 };
            var writer = new Ubicomp.Utils.NET.MulticastTransportFramework.ArrayBufferWriter<byte>();

            serializer.SerializeToWriter(writer, msg, security);
            var bytes = writer.WrittenSpan.ToArray();

            var socketMsg = new SocketMessage(bytes); // 5 is local arrival, 10 is sender seq

            var decoded = serializer.Deserialize(socketMsg, security);

            Assert.NotNull(decoded);
            Assert.Equal(msg.MessageId, decoded.Value.MessageId);
            Assert.Equal(10, decoded.Value.SenderSequenceNumber);


            var decodedVal = decoded.Value;
            serializer.DeserializeContent(ref decodedVal, typeof(TestData));
            var result = (TestData)decodedVal.MessageData;
            Assert.Equal("Data", result.Value);
        }

        private class TestData
        {
            public string Value { get; set; }
        }


    }
}
