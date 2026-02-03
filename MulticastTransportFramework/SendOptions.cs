#nullable enable
using System;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Configuration options for sending a transport message.
    /// </summary>
    public class SendOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether an acknowledgement is requested.
        /// </summary>
        public bool RequestAck { get; set; } = false;

        /// <summary>
        /// Gets or sets the timeout for waiting for an acknowledgement.
        /// If null, the component's default timeout is used.
        /// </summary>
        public TimeSpan? AckTimeout { get; set; }

        /// <summary>
        /// Gets or sets the message type ID. If not specified, the component
        /// will try to look up the ID based on the registered type.
        /// </summary>
        public int? MessageType { get; set; }
    }
}
