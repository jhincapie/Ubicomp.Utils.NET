#nullable enable
using System;
using System.Net;

namespace Ubicomp.Utils.NET.Sockets
{
    /// <summary>
    /// Fluent builder for creating and configuring a <see cref="MulticastSocket"/>.
    /// </summary>
    public class MulticastSocketBuilder
    {
        private MulticastSocketOptions? _options;
        private Action<SocketMessage>? _onMessageReceived;
        private Action<SocketErrorContext>? _onError;
        private Action? _onStarted;

        /// <summary>
        /// Configures the socket for a local network.
        /// </summary>
        public MulticastSocketBuilder WithLocalNetwork(string groupAddress = "239.0.0.1", int port = 5000)
        {
            _options = MulticastSocketOptions.LocalNetwork(groupAddress, port);
            return this;
        }

        /// <summary>
        /// Configures the socket for a wide area network.
        /// </summary>
        public MulticastSocketBuilder WithWideAreaNetwork(string groupAddress = "239.0.0.1", int port = 5000, int ttl = 16)
        {
            _options = MulticastSocketOptions.WideAreaNetwork(groupAddress, port, ttl);
            return this;
        }

        /// <summary>
        /// Sets custom multicast options.
        /// </summary>
        public MulticastSocketBuilder WithOptions(MulticastSocketOptions options)
        {
            _options = options;
            return this;
        }

        /// <summary>
        /// Sets a callback for when a message is received.
        /// </summary>
        public MulticastSocketBuilder OnMessageReceived(Action<SocketMessage> callback)
        {
            _onMessageReceived = callback;
            return this;
        }

        /// <summary>
        /// Sets a callback for when an error occurs.
        /// </summary>
        public MulticastSocketBuilder OnError(Action<SocketErrorContext> callback)
        {
            _onError = callback;
            return this;
        }

        /// <summary>
        /// Sets a callback for when the socket has successfully started.
        /// </summary>
        public MulticastSocketBuilder OnStarted(Action callback)
        {
            _onStarted = callback;
            return this;
        }

        /// <summary>
        /// Builds the <see cref="MulticastSocket"/>.
        /// </summary>
        public MulticastSocket Build()
        {
            if (_options == null)
                throw new InvalidOperationException("Socket options must be configured.");

            var socket = new MulticastSocket(_options)
            {
                OnMessageReceivedAction = _onMessageReceived,
                OnErrorAction = _onError,
                OnStartedAction = _onStarted
            };

            return socket;
        }
    }
}
