using System;
using System.Text.Json.Serialization;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// System message broadcast periodically to announce presence.
    /// </summary>
    [MessageType("sys.heartbeat")]
    public class HeartbeatMessage
    {
        /// <summary>
        /// Unique identifier for the source instance (usually generated GUID).
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable devicename.
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// Processing uptime in seconds.
        /// </summary>
        public double UptimeSeconds
        {
            get; set;
        }

        /// <summary>
        /// Optional metadata (JSON string) for application-specific info.
        /// </summary>
        public string? Metadata
        {
            get; set;
        }
    }
}
