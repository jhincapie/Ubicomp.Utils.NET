#nullable enable
using System;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Provides contextual information for a received transport message.
    /// </summary>
    public class MessageContext
    {
        /// <summary>Gets the unique identifier of the message.</summary>
        public Guid MessageId { get; }

        /// <summary>Gets the source of the message.</summary>
        public EventSource Source { get; }

        /// <summary>Gets the timestamp when the message was sent.</summary>
        public string Timestamp { get; }

        /// <summary>Gets a value indicating whether an acknowledgement was requested.</summary>
        public bool RequestAck { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageContext"/> class.
        /// </summary>
        public MessageContext(Guid messageId, EventSource source, string timestamp, bool requestAck)
        {
            MessageId = messageId;
            Source = source;
            Timestamp = timestamp;
            RequestAck = requestAck;
        }
    }
}
