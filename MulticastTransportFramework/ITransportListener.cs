using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

  public interface ITransportListener
  {

    void MessageReceived(TransportMessage message, String rawMessage);

  }

}
