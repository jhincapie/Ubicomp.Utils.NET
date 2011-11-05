using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jayrock.Json.Conversion;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

  public class TransportMessageImporter : Jayrock.Json.Conversion.IImporter
  {

    private static log4net.ILog logger = log4net.LogManager.GetLogger(typeof(TransportMessageImporter));

    public static Dictionary<Int32, IImporter> Importers = new Dictionary<int, IImporter>();

    public object Import(Jayrock.Json.Conversion.ImportContext context, Jayrock.Json.JsonReader reader)
    {
      TransportMessage tMessage = new TransportMessage();

      try
      {
        reader.Read();
        String nodeName = reader.Text;
        reader.Read();

        do
        {
          if(String.Equals("messageId", nodeName))
          {
            tMessage.MessageId = context.Import<Guid>(reader);
          }
          else if (String.Equals("messageSource", nodeName))
          {
            tMessage.MessageSource = context.Import<EventSource>(reader);
          }
          else if(String.Equals("messageType", nodeName))
          {
            tMessage.MessageType = context.Import<Int32>(reader);
          }
          else if (String.Equals("messageData", nodeName))
          {
            IImporter importer = TransportMessageImporter.Importers[tMessage.MessageType];
            tMessage.MessageData = (ITransportMessageContent)importer.Import(context, reader);
          }
          else if (String.Equals("timeStamp", nodeName))
          {
            tMessage.TimeStamp = context.Import<String>(reader);
          }

          nodeName = reader.Text;
        } while (reader.Read());
      }
      catch (Exception e) 
      {
        logger.Error("The application could not import the message as a valid TransportMessage.", e);
      }

      return tMessage;
    }

    public Type OutputType
    {
      get { return typeof(TransportMessage); }
    }

  }

}
