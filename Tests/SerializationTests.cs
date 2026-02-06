#nullable enable
using System;
using System.IO;
using System.Text.Json;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    /// <summary>
    /// Unit tests for serialization and deserialization within the transport
    /// framework.
    /// </summary>
    [Collection("SharedTransport")]
    public class SerializationTests
    {
        /// <summary>Mock content for serialization tests.</summary>
        [MessageType("test.mockcontent")]
        public class MockContent
        {
            /// <summary>Gets or sets the content text.</summary>
            public string Content { get; set; } = string.Empty;
        }

        /// <summary>
        /// Validates that exporting and importing a message maintains basic
        /// data integrity.
        /// </summary>
        [Fact]
        public void ExportImport_ShouldMaintainData()
        {
            string typeId = "test.message";

            var source = new EventSource(Guid.NewGuid(), "Host", "Desc");
            var content = new MockContent { Content = "Hello" };
            var message = new TransportMessage(source, typeId, content);

            var knownTypes = new System.Collections.Generic.Dictionary<string, Type>();
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            // Export
            string json = JsonSerializer.Serialize(message, options);

            // Import
            var importedMessage =
                JsonSerializer.Deserialize<TransportMessage>(json, options);

            // Assert.NotNull(importedMessage); // Struct is never null
            Assert.Equal(message.MessageId, importedMessage.MessageId);
            Assert.Equal(message.MessageType, importedMessage.MessageType);
        }

        /// <summary>
        /// Validates that exporting and importing a message with a registered
        /// type maintains polymorphic data.
        /// </summary>
        [Fact]
        public void ExportImport_WithRegisteredType_ShouldMaintainData()
        {
            string typeId = "test.mockcontent";

            // Register the type mapping
            var knownTypes = new System.Collections.Generic.Dictionary<string, Type>();
            knownTypes[typeId] = typeof(MockContent);

            var source = new EventSource(Guid.NewGuid(), "Host", "Desc");
            var content = new MockContent { Content = "Polymorphic Hello" };
            var message = new TransportMessage(source, typeId, content);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            // Export
            string json = JsonSerializer.Serialize(message, options);

            // Import
            var importedMessage =
                JsonSerializer.Deserialize<TransportMessage>(json, options);

            // Assert.NotNull(importedMessage); // Struct is never null
            Assert.Equal(message.MessageId, importedMessage.MessageId);
            Assert.Equal(message.MessageType, importedMessage.MessageType);

            // Without converter, MessageData is JsonElement
            Assert.IsType<JsonElement>(importedMessage.MessageData);

            // Manual late deserialization
            var element = (JsonElement)importedMessage.MessageData;
            var data = JsonSerializer.Deserialize(element.GetRawText(), knownTypes[typeId], options);

            Assert.IsType<MockContent>(data);
            Assert.Equal(content.Content, ((MockContent)data!).Content);
        }
    }
}
