#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Ubicomp.Utils.NET.Sockets
{
    /// <summary>
    /// Provides a high-level wrapper around a multicast UDP socket.
    /// Handles joining multicast groups on multiple interfaces and provides
    /// asynchronous sending and receiving capabilities.
    /// </summary>
    public class MulticastSocket : IDisposable
    {
        /// <summary>
        /// Occurs when the socket status changes or a message is received.
        /// </summary>
        public event
            NotifyMulticastSocketListener? OnNotifyMulticastSocketListener;

        /// <summary>The underlying UDP socket.</summary>
        private Socket? _udpSocket;

        /// <summary>A counter for received messages to maintain
        /// order.</summary>
        private int _mConsecutive;

        /// <summary>The local endpoint used for receiving data.</summary>
        private EndPoint _localEndPoint = null!;

        /// <summary>The local IP endpoint representation.</summary>
        private IPEndPoint _localIPEndPoint = null!;

        /// <summary>The target multicast group IP address.</summary>
        private readonly string _targetIP;

        /// <summary>The target port for multicast communication.</summary>
        private readonly int _targetPort;

        /// <summary>The Time-to-Live (TTL) value for multicast
        /// packets.</summary>
        private readonly int _udpTTL;

        /// <summary>The specific local IP address to bind to, if any.</summary>
        private readonly string? _localIP;

        /// <summary>The list of IP addresses successfully joined.</summary>
        private readonly List<IPAddress> _joinedAddresses =
            new List<IPAddress>();

        /// <summary>
        /// Gets the collection of IP addresses that have successfully joined
        /// the multicast group.
        /// </summary>
        public IEnumerable<IPAddress> JoinedAddresses => _joinedAddresses;

        /// <summary>
        /// Initializes a new instance of the <see cref="MulticastSocket"/>
        /// class.
        /// </summary>
        /// <param name="targetIP">The multicast group IP address.</param>
        /// <param name="targetPort">The port to use.</param>
        /// <param name="timeToLive">The multicast Time-to-Live value.</param>
        /// <param name="localIP">An optional local IP to bind to.</param>
        public MulticastSocket(string targetIP, int targetPort, int timeToLive,
                               string? localIP = null)
        {
            _udpSocket = new Socket(AddressFamily.InterNetwork,
                                    SocketType.Dgram, ProtocolType.Udp);
            _mConsecutive = 0;

            _targetIP = targetIP;
            _targetPort = targetPort;
            _udpTTL = timeToLive;
            _localIP = localIP;

            SetupSocket();
        }

        private void SetupSocket()
        {
            if (_udpSocket == null)
                return;

            if (_udpSocket.IsBound)
                throw new ApplicationException(
                    "The socket is already bound and receiving.");

            _localIPEndPoint = new IPEndPoint(IPAddress.Any, _targetPort);
            _localEndPoint = (EndPoint)_localIPEndPoint;

            SetDefaultSocketOptions();

            _udpSocket.Bind(_localIPEndPoint);
            _udpSocket.SetSocketOption(SocketOptionLevel.IP,
                                       SocketOptionName.MulticastTimeToLive,
                                       _udpTTL);

            IPAddress mcastAddr = IPAddress.Parse(_targetIP);
            _joinedAddresses.Clear();

            if (_localIP != null)
                JoinSpecificInterface(mcastAddr, IPAddress.Parse(_localIP));
            else
                JoinAllInterfaces(mcastAddr);

            NotifyListener(MulticastSocketMessageType.SocketStarted);
        }

        private void SetDefaultSocketOptions()
        {
            if (_udpSocket == null)
                return;

            try
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.Udp,
                                           SocketOptionName.NoDelay, 1);
            }
            catch (SocketException)
            {
            }

            _udpSocket.SetSocketOption(SocketOptionLevel.Socket,
                                       SocketOptionName.ReuseAddress, 1);
            _udpSocket.SetSocketOption(SocketOptionLevel.IP,
                                       SocketOptionName.MulticastLoopback, 1);
        }

        private void JoinSpecificInterface(IPAddress mcastAddr,
                                           IPAddress localAddr)
        {
            if (_udpSocket == null)
                return;

            _udpSocket.SetSocketOption(
                SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(mcastAddr, localAddr));
            _joinedAddresses.Add(localAddr);

            try
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP,
                                           SocketOptionName.MulticastInterface,
                                           localAddr.GetAddressBytes());
            }
            catch (SocketException)
            {
            }
        }

        private void JoinAllInterfaces(IPAddress mcastAddr)
        {
            var validAddresses =
                NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 (ni.SupportsMulticast ||
                                  ni.NetworkInterfaceType ==
                                      NetworkInterfaceType.Loopback))
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(addr => addr.Address.AddressFamily ==
                                   AddressFamily.InterNetwork)
                    .Select(addr => addr.Address);

            foreach (var addr in validAddresses)
            {
                try
                {
                    _udpSocket?.SetSocketOption(
                        SocketOptionLevel.IP, SocketOptionName.AddMembership,
                        new MulticastOption(mcastAddr, addr));
                    _joinedAddresses.Add(addr);
                }
                catch (SocketException)
                {
                }
            }

            TryJoinDefault(mcastAddr);
            SetMulticastInterfaceToAny();
        }

        private void TryJoinDefault(IPAddress mcastAddr)
        {
            try
            {
                _udpSocket?.SetSocketOption(
                    SocketOptionLevel.IP, SocketOptionName.AddMembership,
                    new MulticastOption(mcastAddr, IPAddress.Any));
                if (!_joinedAddresses.Any(a => a.Equals(IPAddress.Any)))
                    _joinedAddresses.Add(IPAddress.Any);
            }
            catch (SocketException)
            {
            }
        }

        private void SetMulticastInterfaceToAny()
        {
            try
            {
                _udpSocket?.SetSocketOption(SocketOptionLevel.IP,
                                           SocketOptionName.MulticastInterface,
                                           IPAddress.Any.GetAddressBytes());
            }
            catch (SocketException)
            {
            }
        }

        public void StartReceiving()
        {
            if (OnNotifyMulticastSocketListener == null)
                throw new ApplicationException(
                    "No socket listener has been specified.");

            if (_udpSocket == null)
                throw new ApplicationException("Socket is not initialized.");

            Receive(new StateObject { WorkSocket = _udpSocket });
        }

        private void Receive(StateObject state)
        {
            if (_udpSocket == null)
                return;

            state.WorkSocket.BeginReceiveFrom(
                state.Buffer, 0, StateObject.BufferSize, 0, ref _localEndPoint,
                ReceiveCallback, state);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            if (!(ar.AsyncState is StateObject state))
                return;

            try
            {
                int bytesRead =
                    state.WorkSocket.EndReceiveFrom(ar, ref _localEndPoint);

                byte[] bufferCopy = new byte[bytesRead];
                Array.Copy(state.Buffer, 0, bufferCopy, 0, bytesRead);

                NotifyListener(MulticastSocketMessageType.MessageReceived,
                               bufferCopy, ++_mConsecutive);
                Receive(state);
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.OperationAborted ||
                                             se.SocketErrorCode == SocketError.Interrupted)
            {
                // Expected when socket is closed
            }
            catch (ObjectDisposedException)
            {
                // Expected when socket is closed
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                NotifyListener(MulticastSocketMessageType.ReceiveException, e);
                if (state != null)
                {
                    try
                    {
                        Receive(state);
                    }
                    catch
                    {
                    }
                }
                else
                {
                    try
                    {
                        StartReceiving();
                    }
                    catch
                    {
                    }
                }
            }
        }

        public void Send(string sendData)
        {
            if (_udpSocket == null)
                return;

            byte[] bytesToSend = Encoding.UTF8.GetBytes(sendData);
            var remoteEndPoint =
                new IPEndPoint(IPAddress.Parse(_targetIP), _targetPort);

            _udpSocket.BeginSendTo(bytesToSend, 0, bytesToSend.Length,
                                   SocketFlags.None, remoteEndPoint,
                                   SendCallback, _udpSocket);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                if (!(ar.AsyncState is Socket client))
                    return;

                int bytesSent = client.EndSendTo(ar);
                NotifyListener(MulticastSocketMessageType.MessageSent,
                               bytesSent);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                NotifyListener(MulticastSocketMessageType.SendException, e);
            }
        }

        private void NotifyListener(MulticastSocketMessageType type,
                                    object? obj = null, int consecutive = 0)
        {
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    try
                    {
                        var handler = OnNotifyMulticastSocketListener;
                        if (handler == null)
                            return;

                        handler(this,
                                new NotifyMulticastSocketListenerEventArgs(
                                    type, obj, consecutive));
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e);
                    }
                });
        }

        /// <summary>
        /// Closes the underlying socket and stops any ongoing operations.
        /// </summary>
        public void Close()
        {
            try
            {
                _udpSocket?.Close();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }

        /// <summary>
        /// Disposes the socket resources.
        /// </summary>
        public void Dispose()
        {
            Close();
            _udpSocket?.Dispose();
        }

        internal class StateObject
        {
            public const int BufferSize = 1024;
            public byte[] Buffer { get; } = new byte[BufferSize];
            public Socket WorkSocket { get; set; } = null!;
        }
    }
}
