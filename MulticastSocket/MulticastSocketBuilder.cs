#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

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
        private ILogger? _logger;

        /// <summary>
        /// Configures the socket for a local network.
        /// Automatically attempts to bind to a local LAN interface (192.168.x.x, 10.x.x.x, 172.16-31.x.x).
        /// </summary>
        public MulticastSocketBuilder WithLocalNetwork(string groupAddress = "239.0.0.1", int port = 5000)
        {
            _options = MulticastSocketOptions.LocalNetwork(groupAddress, port);

            // Default to private network filter
            _options.InterfaceFilter = ip =>
            {
                byte[] bytes = ip.GetAddressBytes();
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    // 10.x.x.x
                    if (bytes[0] == 10) return true;
                    // 172.16.x.x - 172.31.x.x
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                    // 192.168.x.x
                    if (bytes[0] == 192 && bytes[1] == 168) return true;
                }
                return false;
            };

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
        /// Binds to a specific network interface matching the predicate.
        /// </summary>
        public MulticastSocketBuilder WithInterface(Func<NetworkInterface, bool> predicate)
        {
            if (_options == null) throw new InvalidOperationException("Options must be initialized before setting interface.");

            var nics = NetworkInterface.GetAllNetworkInterfaces();
            var nic = nics.FirstOrDefault(predicate);

            if (nic != null)
            {
                // Create filter for this NIC's IPs
                var props = nic.GetIPProperties();
                var unicast = props.UnicastAddresses.Select(u => u.Address).ToList();

                _options.InterfaceFilter = ip => unicast.Contains(ip);
                _options.AllowWildcardBinding = false; // Disable wildcard since we picked a specific NIC
            }

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
        /// Sets the logger factory for the socket.
        /// </summary>
        public MulticastSocketBuilder WithLogging(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MulticastSocket>();
            return this;
        }

        /// <summary>
        /// Sets a specific logger for the socket.
        /// </summary>
        public MulticastSocketBuilder WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        /// <summary>
        /// Builds the <see cref="MulticastSocket"/>.
        /// </summary>
        public MulticastSocket Build()
        {
            if (_options == null)
                throw new InvalidOperationException("Socket options must be configured.");

            if (_options.InterfaceFilter == null && _options.AllowWildcardBinding)
            {
                _logger?.LogWarning("MulticastSocket is binding to Wildcard (0.0.0.0). This may leak traffic to all interfaces. Use WithInterface() or WithLocalNetwork() to restrict.");
            }

            var socket = new MulticastSocket(_options, _logger)
            {
                OnMessageReceivedAction = _onMessageReceived,
                OnErrorAction = _onError,
                OnStartedAction = _onStarted
            };

            return socket;
        }
    }
}
