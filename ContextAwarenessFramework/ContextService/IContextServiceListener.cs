using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CAF.ContextService
{
  public interface IContextServiceListener
  {

    void ContextChanged(object sender, NotifyContextServiceListenersEventArgs e);

  }

  public class NotifyContextServiceListenersEventArgs : EventArgs
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

    public NotifyContextServiceListenersEventArgs(Type type, Object newObject)
    {
      this.type = type;
      this.newObject = newObject;
    }

  }

  public delegate void NotifyContextServiceListeners(object sender, NotifyContextServiceListenersEventArgs e);

}
