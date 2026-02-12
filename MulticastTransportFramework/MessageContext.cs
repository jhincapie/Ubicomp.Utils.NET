#nullable enable
using System;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Provides contextual information for a received transport message.
    /// </summary>
    public class MessageContext
    {
        private string? _timestamp;
        private readonly long _ticks;

        /// <summary>Gets the unique identifier of the message.</summary>
        public Guid MessageId { get; }

        /// <summary>Gets the source of the message.</summary>
        public EventSource Source { get; }

        /// <summary>Gets the timestamp when the message was sent.</summary>
        public string Timestamp
        {
            get
            {
                if (_timestamp == null)
                {
                    _timestamp = new DateTime(_ticks).ToString(TransportMessage.DATE_FORMAT_NOW);
                }
                return _timestamp;
            }
        }

        /// <summary>Gets a value indicating whether an acknowledgement was requested.</summary>
        public bool RequestAck { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageContext"/> class using raw ticks.
        /// This is the preferred constructor to avoid string allocations.
        /// </summary>
        public MessageContext(Guid messageId, EventSource source, long ticks, bool requestAck)
        {
            MessageId = messageId;
            Source = source;
            _ticks = ticks;
            RequestAck = requestAck;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageContext"/> class.
        /// </summary>
        [Obsolete("Use the constructor taking 'long ticks' for better performance.")]
        public MessageContext(Guid messageId, EventSource source, string timestamp, bool requestAck)
        {
            MessageId = messageId;
            Source = source;
            _timestamp = timestamp;
            if (DateTime.TryParse(timestamp, out var dt))
            {
                _ticks = dt.Ticks;
            }
            RequestAck = requestAck;
        }
    }
}
