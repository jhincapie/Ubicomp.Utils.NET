using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Multicast.NET.MTF;

namespace Multicast.NET.TestApp
{
  public class MockMessage : ITransportMessageContent
  {

    public String Message { get; set; }

  }
}
