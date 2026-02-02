#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

    /// <summary>
    /// Identifies the source of a transport message.
    /// </summary>
    public class EventSource
    {
        /// <summary>Gets or sets the unique ID of the resource
        /// source.</summary>
        [JsonProperty("resourceId")]
        public Guid ResourceId
        {
            get; set;
        }

        /// <summary>Gets or sets the name of the resource source.</summary>
        [JsonProperty("resourceName")]
        public string ResourceName { get; set; } = string.Empty;

        /// <summary>Gets or sets a friendly name for the source.</summary>
        [JsonProperty("friendlyName")]
        public string? FriendlyName
        {
            get; set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSource"/> class.
        /// </summary>
        public EventSource()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSource"/> class.
        /// </summary>
        /// <param name="resourceId">The source resource ID.</param>
        /// <param name="resourceName">The source resource name.</param>
        public EventSource(Guid resourceId, string resourceName)
        {
            ResourceId = resourceId;
            ResourceName = resourceName;
            FriendlyName = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSource"/> class.
        /// </summary>
        /// <param name="resourceId">The source resource ID.</param>
        /// <param name="resourceName">The source resource name.</param>
        /// <param name="friendlyName">A friendly name for the source.</param>
        public EventSource(Guid resourceId, string resourceName,
                           string? friendlyName)
        {
            ResourceId = resourceId;
            ResourceName = resourceName;
            FriendlyName = friendlyName;
        }
    }

}
