#nullable enable
using System;
using System.IO;
using Newtonsoft.Json;
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
            int typeId = 99;

            var source = new EventSource(Guid.NewGuid(), "Host", "Desc");
            var content = new MockContent { Content = "Hello" };
            var message = new TransportMessage(source, typeId, content);

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new TransportMessageConverter());

            // Export
            string json = JsonConvert.SerializeObject(message, settings);

            // Import
            var importedMessage =
                JsonConvert.DeserializeObject<TransportMessage>(json, settings);

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
            int typeId = 100;

            // Register the type mapping
            TransportMessageConverter.KnownTypes[typeId] = typeof(MockContent);

            var source = new EventSource(Guid.NewGuid(), "Host", "Desc");
            var content = new MockContent { Content = "Polymorphic Hello" };
            var message = new TransportMessage(source, typeId, content);

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new TransportMessageConverter());

            // Export
            string json = JsonConvert.SerializeObject(message, settings);

            // Import
            var importedMessage =
                JsonConvert.DeserializeObject<TransportMessage>(json, settings);

            Assert.NotNull(importedMessage);
            Assert.Equal(message.MessageId, importedMessage!.MessageId);
            Assert.Equal(message.MessageType, importedMessage.MessageType);
            Assert.IsType<MockContent>(importedMessage.MessageData);
            Assert.Equal(content.Content,
                         ((MockContent)importedMessage.MessageData).Content);
        }
    }
}
