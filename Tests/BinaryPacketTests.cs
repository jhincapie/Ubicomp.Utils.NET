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

            // Act
            // Use fully qualified name to avoid ambiguity with internal ArrayBufferWriter
            var writer = new System.Buffers.ArrayBufferWriter<byte>();
            message.SenderSequenceNumber = 123;
            BinaryPacket.SerializeToWriter(writer, message, null, (EncryptorDelegate?)null);
            var packet = writer.WrittenSpan.ToArray();

            // Assert
            Assert.True(packet.Length > 0);
            Assert.Equal((byte)BinaryPacket.MagicByte, packet[0]);
            Assert.Equal((byte)BinaryPacket.ProtocolVersion, packet[1]);
        }

        [Fact]
        public void Deserialize_ShouldRestoreMessage()
        {
            // Arrange
            var source = new EventSource(Guid.NewGuid(), "Host", "Desc");
            var content = new TestMessage { Data = "Roundtrip" };
            var message = new TransportMessage(source, "test.binary", content) { SenderSequenceNumber = 55 };

            // Serialize
            var writer = new System.Buffers.ArrayBufferWriter<byte>();
            BinaryPacket.SerializeToWriter(writer, message, null, (EncryptorDelegate?)null, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var packet = writer.WrittenSpan.ToArray();

            // Act
            // v2 Update: Pass null/empty keys for unencrypted
            var deserialized = BinaryPacket.Deserialize(packet, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }, (DecryptorDelegate?)null, null);

            // Assert
            Assert.NotNull(deserialized);
            var msg = deserialized.Value;

            Assert.Equal(message.MessageId, msg.MessageId);
            Assert.Equal("test.binary", msg.MessageType);

            // Validate payload (lazy deserialization)
            var element = (JsonElement)msg.MessageData;
            var data2 = JsonSerializer.Deserialize<TestMessage>(element.GetRawText(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.Equal("Roundtrip", data2?.Data);
        }
    }
}
