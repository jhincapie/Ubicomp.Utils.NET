#nullable enable
using System;
using System.Buffers;

namespace Ubicomp.Utils.NET.Sockets
{
    /// <summary>
    /// Represents a message received by a multicast socket.
    /// </summary>
    public class SocketMessage : IDisposable
    {
        private byte[]? _rentedBuffer;

        /// <summary>Gets the raw data received.</summary>
        public byte[] Data
        {
            get;
        }

        /// <summary>Gets the length of the valid data in the buffer.</summary>
        public int Length
        {
            get;
        }

        /// <summary>Gets the arrival sequence number of the message.</summary>
        public int ArrivalSequenceId
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
            Length = data.Length;
            ArrivalSequenceId = sequenceId;
            Timestamp = DateTime.Now;
        }

        internal SocketMessage(byte[] buffer, int length, int sequenceId, bool isRented)
        {
            Data = buffer;
            Length = length;
            ArrivalSequenceId = sequenceId;
            Timestamp = DateTime.Now;
            if (isRented)
            {
                _rentedBuffer = buffer;
            }
        }

        public void Dispose()
        {
            if (_rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_rentedBuffer);
                _rentedBuffer = null;
            }
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
