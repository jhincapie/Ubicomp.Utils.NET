using System;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Attribute used to associate a message type identifier with a class.
    /// This identifier is used for automatic registration and routing of messages.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class MessageTypeAttribute : Attribute
    {
        /// <summary>
        /// Gets the unique message type identifier.
        /// </summary>
        public string MsgId
        {
            get;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageTypeAttribute"/> class.
        /// </summary>
        /// <param name="msgId">The unique message type identifier (e.g., "my.message.id").</param>
        public MessageTypeAttribute(string msgId)
        {
            if (string.IsNullOrWhiteSpace(msgId))
            {
                throw new ArgumentException("Message Type ID cannot be null or empty.", nameof(msgId));
            }
            MsgId = msgId;
        }
    }
}
