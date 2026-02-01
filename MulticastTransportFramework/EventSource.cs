using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

  public class EventSource
  {

    private Guid resourceId;
    private string resourceName = string.Empty;
    private string? friendlyName;

    [JsonProperty("resourceId")]
    public Guid ResourceId
    {
      get { return resourceId; }
      set { resourceId = value; }
    }

    [JsonProperty("resourceName")]
    public string ResourceName
    {
      get { return resourceName; }
      set { resourceName = value; }
    }

    [JsonProperty("friendlyName")]
    public string? FriendlyName
    {
      get { return friendlyName; }
      set { friendlyName = value; }
    }

    public EventSource()
    { }

    public EventSource(Guid resourceId, string resourceName)
    {
      this.resourceId = resourceId;
      this.resourceName = resourceName;
      friendlyName = null;
    }

    public EventSource(Guid resourceId, string resourceName, string? friendlyName)
    {
      this.resourceId = resourceId;
      this.resourceName = resourceName;
      this.friendlyName = friendlyName;
    }

  }

}