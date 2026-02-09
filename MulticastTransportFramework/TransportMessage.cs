#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

    /// <summary>
    /// Represents the message envelope used for multicast communication.
    /// </summary>
    public struct TransportMessage
    {
        /// <summary>The date format used for timestamps.</summary>
        public const string DATE_FORMAT_NOW = "yyyy-MM-dd HH:mm:ss";

        /// <summary>Gets or sets the unique identifier for the
        /// message.</summary>
        public Guid MessageId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the sender's sequence number.
        /// Use this for ordering and replay protection.
        /// Serialized in the binary header.
        /// </summary>
        public int SenderSequenceNumber { get; set; } = -1;

        /// <summary>
        /// Gets or sets the local arrival sequence number.
        /// Assigned by the receiver's socket.
        /// NOT serialized.
        /// </summary>
        [JsonIgnore]
        public int ArrivalSequenceNumber { get; set; } = -1;

        /// <summary>Gets or sets the source of the message.</summary>
        public EventSource MessageSource
        {
            get; set;
        }

        /// <summary>Gets or sets the type identifier of the message.</summary>
        public string MessageType
        {
            get; set;
        }

        /// <summary>Gets or sets a value indicating whether an acknowledgement is requested.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool RequestAck
        {
            get; set;
        }

        /// <summary>Gets or sets the actual content of the message.</summary>
        public object MessageData
        {
            get; set;
        }

        /// <summary>Gets or sets the message timestamp.</summary>
        public string TimeStamp
        {
            get; set;
        }



        /// <summary>
        /// Gets or sets a value indicating whether the MessageData is encrypted.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsEncrypted
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the IV/Nonce used for encryption (Base64).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Nonce
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the Authentication Tag for AES-GCM (Base64).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Tag
        {
            get; set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportMessage"/>
        /// struct with content.
        /// </summary>
        /// <param name="source">The source event.</param>
        /// <param name="type">The type of the message.</param>
        /// <param name="data">The message data.</param>
        public TransportMessage(EventSource source, string type, object data)
        {
            MessageId = Guid.NewGuid();
            TimeStamp = DateTime.UtcNow.ToString(DATE_FORMAT_NOW);
            MessageSource = source;
            MessageType = type;
            MessageData = data;

            // Defaults
            RequestAck = false;
            IsEncrypted = false;
            Nonce = null;
            Tag = null;
            SenderSequenceNumber = -1;
            ArrivalSequenceNumber = -1;
        }
    }

}
