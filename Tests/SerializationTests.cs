using System;
using Xunit;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Jayrock.Json.Conversion;
using Jayrock.Json;
using System.IO;

namespace Ubicomp.Utils.NET.Tests
{
    public class SerializationTests
    {
        public class MockContent : ITransportMessageContent
        {
            public string Content { get; set; }
        }

        public class MockExporter : IExporter
        {
            public void Export(ExportContext context, object value, JsonWriter writer)
            {
                var content = (MockContent)value;
                writer.WriteStartObject();
                writer.WriteMember("content");
                writer.WriteString(content.Content);
                writer.WriteEndObject();
            }
            public Type InputType => typeof(MockContent);
        }

        public class MockImporter : IImporter
        {
            public object Import(ImportContext context, JsonReader reader)
            {
                var content = new MockContent();
                reader.Read(); // member
                reader.Read(); // value
                content.Content = reader.Text;
                reader.Read(); // end object
                return content;
            }
            public Type OutputType => typeof(MockContent);
        }

        [Fact]
        public void ExportImport_ShouldMaintainData()
        {
            int typeId = 99;
            TransportMessageExporter.Exporters[typeId] = new MockExporter();
            TransportMessageImporter.Importers[typeId] = new MockImporter();

            var source = new EventSource(Guid.NewGuid(), "Host", "Desc");
            var content = new MockContent { Content = "Hello" };
            var message = new TransportMessage(source, typeId, content);

            // Export
            var writer = new StringWriter();
            var jsonWriter = new JsonTextWriter(writer);
            var exportContext = new ExportContext();
            var exporter = new TransportMessageExporter();
            exporter.Export(exportContext, message, jsonWriter);
            string json = writer.ToString();

            // Import
            var reader = new JsonTextReader(new StringReader(json));
            var importContext = new ImportContext();
            var importer = new TransportMessageImporter();
            var importedMessage = (TransportMessage)importer.Import(importContext, reader);

            Assert.Equal(message.MessageId, importedMessage.MessageId);
            Assert.Equal(message.MessageType, importedMessage.MessageType);
            Assert.Equal(((MockContent)message.MessageData).Content, ((MockContent)importedMessage.MessageData).Content);
        }
    }
}
