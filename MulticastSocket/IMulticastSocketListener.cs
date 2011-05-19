using System;
using System.Collections.Generic;
using System.Text;

namespace Multicast.NET.Sockets
{
  public interface IMulticastSocketListener
  {

    void SocketMessage(object sender, NotifyMulticastSocketListenerEventArgs e);

  }

  public enum MulticastSocketMessageType
  { 
    SocketStarted,
    MessageReceived,
    ReceiveException,
    MessageSent,
    SendException
  }

  public class NotifyMulticastSocketListenerEventArgs : EventArgs
  {
    private MulticastSocketMessageType type;
    private Object newObject;
    private Int32 consecutive;

    public MulticastSocketMessageType Type
    {
      get { return type; }
    }

    public Object NewObject
    {
      get { return newObject; }
    }

    public Int32 Consecutive
    {
      get { return consecutive; }
    }

    public NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType type, Object newObject)
    {
      this.type = type;
      this.newObject = newObject;
    }

    public NotifyMulticastSocketListenerEventArgs(MulticastSocketMessageType type, Object newObject, int mCons)
    {
      this.type = type;
      this.newObject = newObject;
      this.consecutive = mCons;
    }
  }

  public delegate void NotifyMulticastSocketListener(object sender, NotifyMulticastSocketListenerEventArgs e);

}
