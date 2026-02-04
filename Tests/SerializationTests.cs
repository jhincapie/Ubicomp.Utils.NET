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
            options.Converters.Add(new TransportMessageConverter(knownTypes));

            // Export
            string json = JsonSerializer.Serialize(message, options);

            // Import
            var importedMessage =
                JsonSerializer.Deserialize<TransportMessage>(json, options);

            Assert.NotNull(importedMessage);
            Assert.Equal(message.MessageId, importedMessage!.MessageId);
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
            options.Converters.Add(new TransportMessageConverter(knownTypes));

            // Export
            string json = JsonSerializer.Serialize(message, options);

            // Import
            var importedMessage =
                JsonSerializer.Deserialize<TransportMessage>(json, options);

            Assert.NotNull(importedMessage);
            Assert.Equal(message.MessageId, importedMessage!.MessageId);
            Assert.Equal(message.MessageType, importedMessage.MessageType);

            // System.Text.Json deserializes to JsonElement if type is object and not polymorphic handled
            // But here we use TransportMessageConverter which handles known types.
            Assert.IsType<MockContent>(importedMessage.MessageData);
            Assert.Equal(content.Content,
                         ((MockContent)importedMessage.MessageData).Content);
        }
    }
}
