using System;
using System.Collections.Generic;
using System.Text;

namespace CAF.Util.Net
{
  public interface IMulticastSocketListener
  {

    void SocketMessage(object sender, NotifyMulticastSocketListenerEventArgs e);

  }

  public enum MulticastSocketMessageType
  { 
    MessageReceived,
    ReceiveException
  }

  public class NotifyMulticastSocketListenerEventArgs : EventArgs
  {
    private MulticastSocketMessageType type;
    private Object newObject;

    public MulticastSocketMessageType Type
    {
      get { return type; }
      set { type = value; }
    }

    public Object NewObject
    {
      get { return newObject; }
      set { newObject = value; }
    }

    public NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType type, Object newObject)
    {
      this.type = type;
      this.newObject = newObject;
    }

  }

  public delegate void NotifyMulticastSocketListener(object sender, NotifyMulticastSocketListenerEventArgs e);

}
