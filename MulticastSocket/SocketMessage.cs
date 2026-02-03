#nullable enable
using System;

namespace Ubicomp.Utils.NET.Sockets
{
    /// <summary>
    /// Represents a message received by a multicast socket.
    /// </summary>
    public class SocketMessage
    {
        /// <summary>Gets the raw data received.</summary>
        public byte[] Data
        {
            get;
        }

        /// <summary>Gets the sequence number of the message.</summary>
        public int SequenceId
        {
            get;
        }

        /// <summary>Gets the timestamp when the message was received.</summary>
        public DateTime Timestamp
        {
            get;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketMessage"/> class.
        /// </summary>
        public SocketMessage(byte[] data, int sequenceId)
        {
            Data = data;
            SequenceId = sequenceId;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Provides context for socket errors.
    /// </summary>
    public class SocketErrorContext
    {
        /// <summary>Gets a descriptive error message.</summary>
        public string Message
        {
            get;
        }

        /// <summary>Gets the exception associated with the error, if any.</summary>
        public Exception? Exception
        {
            get;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketErrorContext"/> class.
        /// </summary>
        public SocketErrorContext(string message, Exception? exception = null)
        {
            Message = message;
            Exception = exception;
        }
    }
}
