using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.Muticast.TestApp
{
  public class MockMessage : ITransportMessageContent
  {

    public String Message { get; set; }

  }
}
