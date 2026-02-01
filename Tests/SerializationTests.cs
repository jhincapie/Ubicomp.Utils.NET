using System;
using Xunit;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Newtonsoft.Json;
using System.IO;

namespace Ubicomp.Utils.NET.Tests
{
    public class SerializationTests
    {
        public class MockContent : ITransportMessageContent
        {
            public string Content { get; set; }
        }

        [Fact]
        public void ExportImport_ShouldMaintainData()
        {
            int typeId = 99;
            
            var source = new EventSource(Guid.NewGuid(), "Host", "Desc");
            var content = new MockContent { Content = "Hello" };
            var message = new TransportMessage(source, typeId, content);

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new TransportMessageConverter());
            
            // For simple POCO deserialization where we don't have a map, 
            // TransportMessageConverter won't know the type and might produce null data.
            // But checking basic serialization works.

            // Export
            string json = JsonConvert.SerializeObject(message, settings);

            // Import
            // Without registration, MessageData will be null (or fail)
            var importedMessage = JsonConvert.DeserializeObject<TransportMessage>(json, settings);

            Assert.Equal(message.MessageId, importedMessage.MessageId);
            Assert.Equal(message.MessageType, importedMessage.MessageType);
        }

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
            var importedMessage = JsonConvert.DeserializeObject<TransportMessage>(json, settings);

            Assert.Equal(message.MessageId, importedMessage.MessageId);
            Assert.Equal(message.MessageType, importedMessage.MessageType);
            Assert.IsType<MockContent>(importedMessage.MessageData);
            Assert.Equal(content.Content, ((MockContent)importedMessage.MessageData).Content);
        }
    }
}
