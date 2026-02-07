#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ubicomp.Utils.NET.Sockets
{
    /// <summary>
    /// Abstraction for a multicast socket to enable testing and mocking.
    /// </summary>
    public interface IMulticastSocket : IDisposable
    {
        /// <summary>
        /// Gets the collection of IP addresses that have successfully joined the multicast group.
        /// </summary>
        IEnumerable<IPAddress> JoinedAddresses
        {
            get;
        }

        /// <summary>
        /// Event raised when a message is received.
        /// </summary>
        event Action<SocketMessage> OnMessageReceived;

        /// <summary>
        /// Starts the receive loop.
        /// </summary>
        void StartReceiving();

        /// <summary>
        /// Gets an asynchronous stream of messages received by the socket.
        /// </summary>
        IAsyncEnumerable<SocketMessage> GetMessageStream(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a byte array asynchronously.
        /// </summary>
        Task SendAsync(byte[] bytesToSend);

        /// <summary>
        /// Sends a range of bytes asynchronously.
        /// </summary>
        Task SendAsync(byte[] buffer, int offset, int count);

        /// <summary>
        /// Sends a string asynchronously.
        /// </summary>
        Task SendAsync(string sendData);

        /// <summary>
        /// Closes the socket.
        /// </summary>
        void Close();
    }
}
