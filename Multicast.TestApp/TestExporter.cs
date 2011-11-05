using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jayrock.Json.Conversion;

namespace Ubicomp.Utils.NET.Muticast.TestApp
{
  class TestExporter : IExporter
  {
    public void Export(ExportContext context, object value, Jayrock.Json.JsonWriter writer)
    {
      MockMessage mMessage = (MockMessage)value;
      context.Export(mMessage, writer);
    }

    public Type InputType
    {
      get { return typeof(MockMessage); }
    }
  }
}
