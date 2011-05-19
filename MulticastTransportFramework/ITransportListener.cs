using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Multicast.NET.MTF
{

  public interface ITransportListener
  {

    void MessageReceived(TransportMessage message, String rawMessage);

  }

}
