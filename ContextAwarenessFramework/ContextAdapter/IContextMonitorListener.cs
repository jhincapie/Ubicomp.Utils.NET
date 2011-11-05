using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CAF.ContextAdapter
{

  public interface IContextMonitorListener
  {

    void UpdateMonitorReading(object sender, NotifyContextMonitorListenersEventArgs e);

  }

  public class NotifyContextMonitorListenersEventArgs : EventArgs
  {
    private Type type;
    private Object newObject;

    public Type Type
    {
      get { return type; }
      set { type = value; }
    }

    public Object NewObject
    {
      get { return newObject; }
      set { newObject = value; }
    }

    public NotifyContextMonitorListenersEventArgs(Type type, Object newObject)
    {
      this.type = type;
      this.newObject = newObject;
    }

  }

  public delegate void NotifyContextMonitorListeners(object sender, NotifyContextMonitorListenersEventArgs e);

}
