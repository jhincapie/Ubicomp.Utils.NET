using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jayrock.Json.Conversion;

namespace Multicast.NET.TestApp
{
  class TestImporter : IImporter
  {
    public object Import(ImportContext context, Jayrock.Json.JsonReader reader)
    {
      MockMessage mMessage = context.Import<MockMessage>(reader);
      return mMessage;
    }

    public Type OutputType
    {
      get { return typeof(MockMessage); }
    }
  }
}
