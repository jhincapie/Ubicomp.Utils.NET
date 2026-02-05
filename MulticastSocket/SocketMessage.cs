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

        /// <summary>
        /// Callback action to return this instance to a pool.
        /// </summary>
        internal Action<SocketMessage>? ReturnCallback { get; set; }

        /// <summary>Gets the raw data received.</summary>
        public byte[] Data
        {
            get; private set;
        }

        /// <summary>Gets the length of the valid data in the buffer.</summary>
        public int Length
        {
            get; private set;
        }

        /// <summary>Gets the arrival sequence number of the message.</summary>
        public int ArrivalSequenceId
        {
            get; private set;
        }

        /// <summary>Gets the timestamp when the message was received.</summary>
        public DateTime Timestamp
        {
            get; private set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketMessage"/> class.
        /// </summary>
        public SocketMessage()
        {
            Data = Array.Empty<byte>();
            Length = 0;
            ArrivalSequenceId = 0;
            Timestamp = DateTime.MinValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketMessage"/> class.
        ///For backward compatibility/testing.
        /// </summary>
        public SocketMessage(byte[] data, int sequenceId)
        {
            Data = data;
            Length = data.Length;
            ArrivalSequenceId = sequenceId;
            Timestamp = DateTime.Now;
        }

        internal void Reset(byte[] buffer, int length, int sequenceId, bool isRented)
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

            var callback = ReturnCallback;
            if (callback != null)
            {
                ReturnCallback = null;
                callback.Invoke(this);
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
