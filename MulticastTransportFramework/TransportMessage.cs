#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

    /// <summary>
    /// Represents the message envelope used for multicast communication.
    /// </summary>
    public class TransportMessage
    {
        /// <summary>The date format used for timestamps.</summary>
        public const string DATE_FORMAT_NOW = "yyyy-MM-dd HH:mm:ss";

        /// <summary>Gets or sets the unique identifier for the
        /// message.</summary>
        public Guid MessageId
        {
            get; set;
        }

        /// <summary>Gets or sets the source of the message.</summary>
        public EventSource MessageSource { get; set; } = null!;

        /// <summary>Gets or sets the type identifier of the message.</summary>
        public int MessageType
        {
            get; set;
        }

        /// <summary>Gets or sets the actual content of the message.</summary>
        public ITransportMessageContent MessageData { get; set; } = null!;

        /// <summary>Gets or sets the message timestamp.</summary>
        public string TimeStamp { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportMessage"/>
        /// class.
        /// </summary>
        public TransportMessage()
        {
            MessageId = Guid.NewGuid();
            SetTimeStamp();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportMessage"/>
        /// class with content.
        /// </summary>
        /// <param name="source">The source event.</param>
        /// <param name="type">The type of the message.</param>
        /// <param name="data">The message data.</param>
        public TransportMessage(EventSource source, int type,
                                ITransportMessageContent data)
            : this()
        {
            MessageSource = source;
            MessageType = type;
            MessageData = data;
        }

        private void SetTimeStamp()
        {
            TimeStamp = DateTime.Now.ToString(DATE_FORMAT_NOW);
        }
    }

}
