#nullable enable
using System;
using System.Net;

namespace Ubicomp.Utils.NET.Sockets
{
    /// <summary>
    /// Encapsulates configuration options for a <see cref="MulticastSocket"/>.
    /// </summary>
    public class MulticastSocketOptions
    {
        /// <summary>The multicast group IP address.</summary>
        public string GroupAddress
        {
            get; set;
        }

        /// <summary>The port for multicast communication.</summary>
        public int Port
        {
            get; set;
        }

        /// <summary>The multicast Time-to-Live (TTL) value.</summary>
        public int TimeToLive { get; set; } = 1;

        /// <summary>The specific local IP address to bind to, if any.</summary>
        public string? LocalIP
        {
            get; set;
        }

        /// <summary>Whether to allow multiple sockets to bind to the same address and port.</summary>
        public bool ReuseAddress { get; set; } = true;

        /// <summary>Whether multicast packets are looped back to the sending interface.</summary>
        public bool MulticastLoopback { get; set; } = true;

        /// <summary>Whether to disable the Nagle algorithm for low-latency sending.</summary>
        public bool NoDelay { get; set; } = true;

        /// <summary>Whether to set the Don't Fragment flag on IP packets.</summary>
        public bool DontFragment { get; set; } = false;

        /// <summary>The size of the receive buffer in bytes. 0 uses the OS default.</summary>
        public int ReceiveBufferSize { get; set; } = 0;

        /// <summary>The size of the send buffer in bytes. 0 uses the OS default.</summary>
        public int SendBufferSize { get; set; } = 0;

        /// <summary>An optional filter to select which network interfaces to join.</summary>
        public Func<IPAddress, bool>? InterfaceFilter
        {
            get; set;
        }

        /// <summary>Whether to automatically join the multicast group upon socket startup.</summary>
        public bool AutoJoin { get; set; } = true;

        /// <summary>
        /// Whether to enforce strict message ordering via the GateKeeper.
        /// When true, messages are processed sequentially based on sequence IDs.
        /// When false (default), messages are processed immediately as they arrive.
        /// </summary>
        public bool EnforceOrdering { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="MulticastSocketOptions"/> class.
        /// </summary>
        /// <param name="groupAddress">The multicast group IP address.</param>
        /// <param name="port">The port to use.</param>
        private MulticastSocketOptions(string groupAddress, int port)
        {
            GroupAddress = groupAddress;
            Port = port;
        }

        /// <summary>
        /// Creates options for a local network with sensible defaults (TTL=1).
        /// </summary>
        /// <param name="groupAddress">The multicast group IP address (default: 239.0.0.1).</param>
        /// <param name="port">The port to use (default: 5000).</param>
        /// <returns>A new instance of <see cref="MulticastSocketOptions"/>.</returns>
        public static MulticastSocketOptions LocalNetwork(string groupAddress = "239.0.0.1", int port = 5000)
        {
            var options = new MulticastSocketOptions(groupAddress, port)
            {
                TimeToLive = 1
            };
            options.Validate();
            return options;
        }

        /// <summary>
        /// Creates options for a wide area network with sensible defaults (TTL=16).
        /// </summary>
        /// <param name="groupAddress">The multicast group IP address (default: 239.0.0.1).</param>
        /// <param name="port">The port to use (default: 5000).</param>
        /// <param name="ttl">The Time-to-Live value (default: 16).</param>
        /// <returns>A new instance of <see cref="MulticastSocketOptions"/>.</returns>
        public static MulticastSocketOptions WideAreaNetwork(string groupAddress = "239.0.0.1", int port = 5000, int ttl = 16)
        {
            var options = new MulticastSocketOptions(groupAddress, port)
            {
                TimeToLive = ttl
            };
            options.Validate();
            return options;
        }

        /// <summary>
        /// Validates the current options.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if options are invalid.</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(GroupAddress))
                throw new ArgumentException("Group address cannot be empty.", nameof(GroupAddress));

            if (!IPAddress.TryParse(GroupAddress, out var ip) || !IsMulticast(ip))
                throw new ArgumentException($"'{GroupAddress}' is not a valid multicast IP address.", nameof(GroupAddress));

            if (Port < 1 || Port > 65535)
                throw new ArgumentException("Port must be between 1 and 65535.", nameof(Port));

            if (TimeToLive < 0 || TimeToLive > 255)
                throw new ArgumentException("TTL must be between 0 and 255.", nameof(TimeToLive));
        }

        private static bool IsMulticast(IPAddress ip)
        {
            byte firstOctet = ip.GetAddressBytes()[0];
            return firstOctet >= 224 && firstOctet <= 239;
        }
    }
}
