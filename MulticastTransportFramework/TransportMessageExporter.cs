using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jayrock.Json;
using Jayrock.Json.Conversion;

namespace Multicast.NET.MTF
{
  public class TransportMessageExporter : IExporter
  {

    public static Dictionary<Int32, IExporter> Exporters = new Dictionary<int, IExporter>();

    public void Export(ExportContext context, object value, JsonWriter writer)
    {
      TransportMessage tMessage = (TransportMessage)value;
      IExporter contentExporter = Exporters[tMessage.MessageType];

      writer.WriteStartObject();

      writer.WriteMember("messageId");
      context.Export(tMessage.MessageId, writer);

      writer.WriteMember("messageSource");
      context.Export(tMessage.MessageSource, writer);

      writer.WriteMember("messageType");
      context.Export(tMessage.MessageType, writer);

      writer.WriteMember("messageData");
      contentExporter.Export(context, tMessage.MessageData, writer);      

      writer.WriteMember("timeStamp");
      context.Export(tMessage.TimeStamp, writer);

      writer.WriteEndObject();
    }

    public Type InputType
    {
      get { return typeof(TransportMessage); }
    }

  }
}
