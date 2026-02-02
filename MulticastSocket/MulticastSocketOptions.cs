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
        public string TargetIP { get; set; }

        /// <summary>The port for multicast communication.</summary>
        public int TargetPort { get; set; }

        /// <summary>The multicast Time-to-Live (TTL) value.</summary>
        public int TimeToLive { get; set; } = 1;

        /// <summary>The specific local IP address to bind to, if any.</summary>
        public string? LocalIP { get; set; }

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
        public Func<IPAddress, bool>? InterfaceFilter { get; set; }

        /// <summary>Whether to automatically join the multicast group upon socket startup.</summary>
        public bool AutoJoin { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="MulticastSocketOptions"/> class.
        /// </summary>
        /// <param name="targetIP">The multicast group IP address.</param>
        /// <param name="targetPort">The port to use.</param>
        public MulticastSocketOptions(string targetIP, int targetPort)
        {
            TargetIP = targetIP;
            TargetPort = targetPort;
        }

        /// <summary>
        /// Validates the current options.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if options are invalid.</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(TargetIP))
                throw new ArgumentException("Target IP cannot be empty.", nameof(TargetIP));

            if (!IPAddress.TryParse(TargetIP, out var ip) || !IsMulticast(ip))
                throw new ArgumentException($"'{TargetIP}' is not a valid multicast IP address.", nameof(TargetIP));

            if (TargetPort < 1 || TargetPort > 65535)
                throw new ArgumentException("Target port must be between 1 and 65535.", nameof(TargetPort));

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
