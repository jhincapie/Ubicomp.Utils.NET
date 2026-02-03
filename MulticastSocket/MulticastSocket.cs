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
        private Socket? _udpSocket;
        private int _mConsecutive;
        private EndPoint _localEndPoint = null!;
        private IPEndPoint _localIPEndPoint = null!;
        private readonly MulticastSocketOptions _options;
        private readonly List<IPAddress> _joinedAddresses = new List<IPAddress>();

        internal Action<SocketMessage>? OnMessageReceivedAction { get; set; }
        internal Action<SocketErrorContext>? OnErrorAction { get; set; }
        internal Action? OnStartedAction { get; set; }

        /// <summary>
        /// Gets the collection of IP addresses that have successfully joined the multicast group.
        /// </summary>
        public IEnumerable<IPAddress> JoinedAddresses => _joinedAddresses;

        /// <summary>
        /// Initializes a new instance of the <see cref="MulticastSocket"/> class.
        /// </summary>
        internal MulticastSocket(MulticastSocketOptions options)
        {
            options.Validate();
            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _mConsecutive = 0;
            _options = options;

            SetupSocket();
        }

        private void SetupSocket()
        {
            if (_udpSocket == null) return;
            if (_udpSocket.IsBound) throw new ApplicationException("The socket is already bound.");

            _localIPEndPoint = new IPEndPoint(IPAddress.Any, _options.Port);
            _localEndPoint = (EndPoint)_localIPEndPoint;

            SetDefaultSocketOptions();

            _udpSocket.Bind(_localIPEndPoint);
            _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, _options.TimeToLive);

            IPAddress mcastAddr = IPAddress.Parse(_options.GroupAddress);
            _joinedAddresses.Clear();

            if (_options.LocalIP != null)
                JoinSpecificInterface(mcastAddr, IPAddress.Parse(_options.LocalIP));
            else if (_options.AutoJoin)
                JoinAllInterfaces(mcastAddr);

            OnStartedAction?.Invoke();
        }

        private void SetDefaultSocketOptions()
        {
            if (_udpSocket == null) return;

            try
            {
                if (_options.NoDelay)
                    _udpSocket.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.NoDelay, 1);
            }
            catch (SocketException) { }

            if (_options.ReuseAddress)
                _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

            if (_options.MulticastLoopback)
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, 1);

            if (_options.DontFragment)
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment, 1);

            if (_options.ReceiveBufferSize > 0)
                _udpSocket.ReceiveBufferSize = _options.ReceiveBufferSize;

            if (_options.SendBufferSize > 0)
                _udpSocket.SendBufferSize = _options.SendBufferSize;
        }

        private void JoinSpecificInterface(IPAddress mcastAddr, IPAddress localAddr)
        {
            if (_udpSocket == null) return;

            _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mcastAddr, localAddr));
            _joinedAddresses.Add(localAddr);

            try
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localAddr.GetAddressBytes());
            }
            catch (SocketException) { }
        }

        private void JoinAllInterfaces(IPAddress mcastAddr)
        {
            var validAddresses = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 (ni.SupportsMulticast || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback))
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(addr => addr.Address);

            foreach (var addr in validAddresses)
            {
                if (_options.InterfaceFilter != null && !_options.InterfaceFilter(addr)) continue;

                try
                {
                    _udpSocket?.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mcastAddr, addr));
                    _joinedAddresses.Add(addr);
                }
                catch (SocketException) { }
            }

            if (_options.InterfaceFilter == null || _options.InterfaceFilter(IPAddress.Any))
            {
                TryJoinDefault(mcastAddr);
            }
            SetMulticastInterfaceToAny();
        }

        private void TryJoinDefault(IPAddress mcastAddr)
        {
            try
            {
                _udpSocket?.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mcastAddr, IPAddress.Any));
                if (!_joinedAddresses.Any(a => a.Equals(IPAddress.Any)))
                    _joinedAddresses.Add(IPAddress.Any);
            }
            catch (SocketException) { }
        }

        private void SetMulticastInterfaceToAny()
        {
            try
            {
                _udpSocket?.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, IPAddress.Any.GetAddressBytes());
            }
            catch (SocketException) { }
        }

        public void StartReceiving()
        {
            if (_udpSocket == null) throw new ApplicationException("Socket is not initialized.");
            Receive(new StateObject { WorkSocket = _udpSocket });
        }

        private void Receive(StateObject state)
        {
            if (_udpSocket == null) return;
            state.WorkSocket.BeginReceiveFrom(state.Buffer, 0, StateObject.BufferSize, 0, ref _localEndPoint, ReceiveCallback, state);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            if (!(ar.AsyncState is StateObject state)) return;

            try
            {
                int bytesRead = state.WorkSocket.EndReceiveFrom(ar, ref _localEndPoint);
                byte[] bufferCopy = new byte[bytesRead];
                Array.Copy(state.Buffer, 0, bufferCopy, 0, bytesRead);

                int seqId = Interlocked.Increment(ref _mConsecutive);
                var msg = new SocketMessage(bufferCopy, seqId);
                OnMessageReceivedAction?.Invoke(msg);

                Receive(state);
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.OperationAborted || se.SocketErrorCode == SocketError.Interrupted) { }
            catch (ObjectDisposedException) { }
            catch (Exception e)
            {
                OnErrorAction?.Invoke(new SocketErrorContext("Error during receive.", e));
                try { Receive(state); } catch { }
            }
        }

        public void Send(string sendData)
        {
            if (_udpSocket == null) return;
            byte[] bytesToSend = Encoding.UTF8.GetBytes(sendData);
            Send(bytesToSend);
        }

        public void Send(byte[] bytesToSend)
        {
            if (_udpSocket == null) return;
            var remoteEndPoint = new IPEndPoint(IPAddress.Parse(_options.GroupAddress), _options.Port);
            _udpSocket.BeginSendTo(bytesToSend, 0, bytesToSend.Length, SocketFlags.None, remoteEndPoint, SendCallback, _udpSocket);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                if (!(ar.AsyncState is Socket client)) return;
                client.EndSendTo(ar);
            }
            catch (Exception e)
            {
                OnErrorAction?.Invoke(new SocketErrorContext("Error during send.", e));
            }
        }

        public void Close()
        {
            try { _udpSocket?.Close(); } catch { }
        }

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
