#nullable enable
using System;
using System.Text;
using System.Text.Json;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("SharedTransport")]
    public class BinaryPacketTests
    {
        [MessageType("test.binary")]
        public class TestMessage
        {
            public string Data { get; set; } = "";
        }

        [Fact]
        public void Serialize_ShouldCreateValidPacket()
        {
            // Arrange
            var source = new EventSource(Guid.NewGuid(), "Host", "Desc");
            var content = new TestMessage { Data = "Binary Payload" };
            var message = new TransportMessage(source, "test.binary", content);
            var payload = JsonSerializer.SerializeToUtf8Bytes(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            // Act
            byte[] packet = BinaryPacket.Serialize(message, 123, null, null, payload);

            // Assert
            Assert.True(packet.Length > 0);
            Assert.Equal(BinaryPacket.MagicByte, packet[0]);
            Assert.Equal(BinaryPacket.ProtocolVersion, packet[1]);
        }

        [Fact]
        public void Deserialize_ShouldRestoreMessage()
        {
            // Arrange
            var source = new EventSource(Guid.NewGuid(), "Host", "Desc");
            var content = new TestMessage { Data = "Roundtrip" };
            var message = new TransportMessage(source, "test.binary", content);
            var payload = JsonSerializer.SerializeToUtf8Bytes(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

             // Serialize
            byte[] packet = BinaryPacket.Serialize(message, 55, null, null, payload);

            // Act
            var deserialized = BinaryPacket.Deserialize(packet, 55, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(message.MessageId, deserialized!.MessageId);
            Assert.Equal("test.binary", deserialized.MessageType);

            // Validate payload (lazy deserialization)
            var element = (JsonElement)deserialized.MessageData;
            var data2 = JsonSerializer.Deserialize<TestMessage>(element.GetRawText(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal("Roundtrip", data2?.Data);
        }
    }
}
