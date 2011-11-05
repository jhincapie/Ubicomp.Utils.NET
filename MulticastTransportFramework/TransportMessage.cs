using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

  public class TransportMessage
  {

    public const String DATE_FORMAT_NOW = "yyyy-MM-dd HH:mm:ss";

    private Guid messageId;
    private EventSource messageSource;
    private int messageType;
    private ITransportMessageContent messageData;
    private String timeStamp;

    public Guid MessageId
    {
      get { return messageId; }
      set { messageId = value; }
    }

    public EventSource MessageSource
    {
      get { return messageSource; }
      set { messageSource = value; }
    }

    public int MessageType
    {
      get { return messageType; }
      set { messageType = value; }
    }

    public ITransportMessageContent MessageData
    {
      get { return messageData; }
      set { messageData = value; }
    }

    public String TimeStamp
    {
      get { return timeStamp; }
      set { timeStamp = value; }
    }

    public TransportMessage()
    {
      messageId = Guid.NewGuid();
      SetTimeStamp();
    }

    public TransportMessage(EventSource es, int mt, ITransportMessageContent md) : this()
    {
      messageSource = es;
      messageType = mt;
      messageData = md;
    }

    private void SetTimeStamp()
    {
      timeStamp = DateTime.Now.ToString(DATE_FORMAT_NOW);
    }

  }

}
