using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jayrock.Json.Conversion;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

  public class EventSource
  {

    private Guid resourceId;
    private String resourceName;
    private String friendlyName;

    [JsonMemberName("resourceId")]
    public Guid ResourceId
    {
      get { return resourceId; }
      set { resourceId = value; }
    }

    [JsonMemberName("resourceName")]
    public String ResourceName
    {
      get { return resourceName; }
      set { resourceName = value; }
    }

    [JsonMemberName("friendlyName")]
    public String FriendlyName
    {
      get { return friendlyName; }
      set { friendlyName = value; }
    }

    public EventSource()
    { }

    public EventSource(Guid resourceId, String resourceName)
    {
      this.resourceId = resourceId;
      this.resourceName = resourceName;
      friendlyName = null;
    }

    public EventSource(Guid resourceId, String resourceName, String friendlyName)
    {
      this.resourceId = resourceId;
      this.resourceName = resourceName;
      this.friendlyName = friendlyName;
    }

  }

}
