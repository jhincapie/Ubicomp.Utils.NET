#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        private readonly object _joinedLock = new object();
        private readonly Channel<SocketMessage> _messageChannel = Channel.CreateUnbounded<SocketMessage>();
        private bool _isChannelStarted = false;
#if NET8_0_OR_GREATER
        private CancellationTokenSource? _receiveCts;
#endif

        /// <summary>Gets or sets the logger for this component.</summary>
        public ILogger Logger { get; set; } = NullLogger.Instance;

        internal Action<SocketMessage>? OnMessageReceivedAction
        {
            get; set;
        }
        internal Action<SocketErrorContext>? OnErrorAction
        {
            get; set;
        }
        internal Action? OnStartedAction
        {
            get; set;
        }

        /// <summary>
        /// Gets the collection of IP addresses that have successfully joined the multicast group.
        /// </summary>
        public IEnumerable<IPAddress> JoinedAddresses
        {
            get
            {
                lock (_joinedLock)
                {
                    return _joinedAddresses.ToList();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MulticastSocket"/> class.
        /// </summary>
        internal MulticastSocket(MulticastSocketOptions options, ILogger? logger = null)
        {
            if (logger != null)
            {
                Logger = logger;
            }
            options.Validate();
            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _mConsecutive = 0;
            _options = options;

            SetupSocket();
        }

        private void SetupSocket()
        {
            if (_udpSocket == null)
                return;
            if (_udpSocket.IsBound)
                throw new ApplicationException("The socket is already bound.");

            Logger.LogDebug("Setting up MulticastSocket on port {Port}", _options.Port);

            _localIPEndPoint = new IPEndPoint(IPAddress.Any, _options.Port);
            _localEndPoint = (EndPoint)_localIPEndPoint;

            SetDefaultSocketOptions();

            try
            {
                _udpSocket.Bind(_localIPEndPoint);
                Logger.LogInformation("Socket bound to {EndPoint}", _localIPEndPoint);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Failed to bind socket to port {Port}", _options.Port);
                throw;
            }

            _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, _options.TimeToLive);
            Logger.LogDebug("Multicast TTL set to {TTL}", _options.TimeToLive);

            IPAddress mcastAddr = IPAddress.Parse(_options.GroupAddress);
            lock (_joinedLock)
            {
                _joinedAddresses.Clear();
            }

            if (_options.LocalIP != null)
            {
                Logger.LogInformation("Joining specific interface: {LocalIP}", _options.LocalIP);
                JoinSpecificInterface(mcastAddr, IPAddress.Parse(_options.LocalIP));
            }
            else
            {
                Logger.LogInformation("Auto-joining all valid interfaces for group {GroupAddress}", _options.GroupAddress);
                JoinAllInterfaces(mcastAddr);
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            }

            OnStartedAction?.Invoke();
        }

        private void SetDefaultSocketOptions()
        {
            if (_udpSocket == null)
                return;

            try
            {
                if (_options.NoDelay)
                {
                    _udpSocket.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.NoDelay, 1);
                    Logger.LogTrace("Socket option NoDelay set to true");
                }
            }
            catch (SocketException ex)
            {
                Logger.LogWarning(ex, "Failed to set NoDelay socket option");
            }

            if (_options.ReuseAddress)
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                Logger.LogTrace("Socket option ReuseAddress set to true");
            }

            if (_options.MulticastLoopback)
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, 1);
                Logger.LogTrace("Socket option MulticastLoopback set to true");
            }
            else
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, 0);
                Logger.LogTrace("Socket option MulticastLoopback set to false");
            }

            if (_options.DontFragment)
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment, 1);
                Logger.LogTrace("Socket option DontFragment set to true");
            }

            if (_options.ReceiveBufferSize > 0)
            {
                _udpSocket.ReceiveBufferSize = _options.ReceiveBufferSize;
                Logger.LogTrace("ReceiveBufferSize set to {Size}", _options.ReceiveBufferSize);
            }

            if (_options.SendBufferSize > 0)
            {
                _udpSocket.SendBufferSize = _options.SendBufferSize;
                Logger.LogTrace("SendBufferSize set to {Size}", _options.SendBufferSize);
            }
        }

        private void JoinSpecificInterface(IPAddress mcastAddr, IPAddress localAddr)
        {
            if (_udpSocket == null)
                return;

            try
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mcastAddr, localAddr));
                lock (_joinedLock)
                {
                    _joinedAddresses.Add(localAddr);
                }
                Logger.LogInformation("Successfully joined multicast group {Group} on interface {Interface}", mcastAddr, localAddr);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to join multicast group {Group} on interface {Interface}", mcastAddr, localAddr);
            }

            try
            {
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localAddr.GetAddressBytes());
                Logger.LogDebug("Multicast interface set to {Interface}", localAddr);
            }
            catch (SocketException ex)
            {
                Logger.LogWarning(ex, "Failed to set MulticastInterface to {Interface}", localAddr);
            }
        }

        private void OnNetworkAddressChanged(object sender, EventArgs e)
        {
            Logger.LogInformation("Network address changed detected. Attempting to join new interfaces.");
            try
            {
                IPAddress mcastAddr = IPAddress.Parse(_options.GroupAddress);
                JoinAllInterfaces(mcastAddr);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while handling network address change.");
            }
        }

        private void JoinAllInterfaces(IPAddress mcastAddr)
        {
            var validAddresses = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 (ni.SupportsMulticast || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback))
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(addr => addr.Address);

            int joinCount = 0;
            foreach (var addr in validAddresses)
            {
                if (_options.InterfaceFilter != null && !_options.InterfaceFilter(addr))
                {
                    Logger.LogTrace("Interface {Interface} filtered out by InterfaceFilter", addr);
                    continue;
                }

                lock (_joinedLock)
                {
                    if (_joinedAddresses.Contains(addr))
                        continue;
                }

                try
                {
                    _udpSocket?.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mcastAddr, addr));
                    lock (_joinedLock)
                    {
                        _joinedAddresses.Add(addr);
                    }
                    joinCount++;
                    Logger.LogDebug("Joined multicast group {Group} on interface {Interface}", mcastAddr, addr);
                }
                catch (SocketException ex)
                {
                    Logger.LogWarning("Failed to join multicast group {Group} on interface {Interface}: {Message}", mcastAddr, addr, ex.Message);
                }
            }

            Logger.LogInformation("Joined multicast group on {Count} interfaces", joinCount);

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
                lock (_joinedLock)
                {
                    if (!_joinedAddresses.Any(a => a.Equals(IPAddress.Any)))
                    {
                        _joinedAddresses.Add(IPAddress.Any);
                        Logger.LogDebug("Joined multicast group {Group} on IPAddress.Any (Default)", mcastAddr);
                    }
                }
            }
            catch (SocketException ex)
            {
                Logger.LogWarning("Failed to join multicast group {Group} on IPAddress.Any: {Message}", mcastAddr, ex.Message);
            }
        }

        private void SetMulticastInterfaceToAny()
        {
            try
            {
                _udpSocket?.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, IPAddress.Any.GetAddressBytes());
                Logger.LogTrace("Multicast interface set to IPAddress.Any");
            }
            catch (SocketException ex)
            {
                Logger.LogTrace("Failed to set MulticastInterface to IPAddress.Any: {Message}", ex.Message);
            }
        }

        public void StartReceiving()
        {
            if (_udpSocket == null)
                throw new ApplicationException("Socket is not initialized.");

            Logger.LogInformation("Starting receive loop...");
#if NET8_0_OR_GREATER
            _receiveCts = new CancellationTokenSource();
            _ = ReceiveAsyncLoop(_receiveCts.Token);
#else
            Receive(new StateObject { WorkSocket = _udpSocket });
#endif
        }

#if NET8_0_OR_GREATER
        private async Task ReceiveAsyncLoop(CancellationToken cancellationToken)
        {
            if (_udpSocket == null) return;

            byte[] buffer = new byte[StateObject.BufferSize];
            Memory<byte> memoryBuffer = buffer;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpSocket.ReceiveFromAsync(memoryBuffer, SocketFlags.None, _localEndPoint, cancellationToken);

                        // Copy the data as in the original implementation
                        byte[] bufferCopy = new byte[result.ReceivedBytes];
                        Array.Copy(buffer, 0, bufferCopy, 0, result.ReceivedBytes);

                        int seqId = Interlocked.Increment(ref _mConsecutive);
                        var msg = new SocketMessage(bufferCopy, seqId);

                        Logger.LogTrace("Received message with SeqId {SeqId}, Length {Length}", seqId, result.ReceivedBytes);

                        OnMessageReceivedAction?.Invoke(msg);
                        _messageChannel.Writer.TryWrite(msg);
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.OperationAborted || se.SocketErrorCode == SocketError.Interrupted)
                    {
                        Logger.LogInformation("Receive operation aborted or interrupted.");
                        break;
                    }
                     catch (OperationCanceledException)
                    {
                        Logger.LogDebug("Receive operation cancelled.");
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        Logger.LogDebug("Socket disposed, stopping receive loop.");
                        break;
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Error during receive.");
                        OnErrorAction?.Invoke(new SocketErrorContext("Error during receive.", e));
                    }
                }
            }
            catch (Exception ex)
            {
                 // Outer catch just in case
                 Logger.LogError(ex, "Fatal error in receive loop.");
            }
        }
#endif

        private void Receive(StateObject state)
        {
            if (_udpSocket == null)
                return;
            state.WorkSocket.BeginReceiveFrom(state.Buffer, 0, StateObject.BufferSize, 0, ref _localEndPoint, ReceiveCallback, state);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            if (!(ar.AsyncState is StateObject state))
                return;

            try
            {
                int bytesRead = state.WorkSocket.EndReceiveFrom(ar, ref _localEndPoint);
                byte[] bufferCopy = new byte[bytesRead];
                Array.Copy(state.Buffer, 0, bufferCopy, 0, bytesRead);

                int seqId = Interlocked.Increment(ref _mConsecutive);
                var msg = new SocketMessage(bufferCopy, seqId);

                Logger.LogTrace("Received message with SeqId {SeqId}, Length {Length}", seqId, bytesRead);

                OnMessageReceivedAction?.Invoke(msg);
                _messageChannel.Writer.TryWrite(msg);

                Receive(state);
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.OperationAborted || se.SocketErrorCode == SocketError.Interrupted)
            {
                Logger.LogInformation("Receive operation aborted or interrupted.");
            }
            catch (ObjectDisposedException)
            {
                Logger.LogDebug("Socket disposed, stopping receive loop.");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error during receive.");
                OnErrorAction?.Invoke(new SocketErrorContext("Error during receive.", e));
                try
                {
                    Receive(state);
                }
                catch { }
            }
        }

        /// <summary>
        /// Gets an asynchronous stream of messages received by the socket.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to stop the stream.</param>
        /// <returns>An asynchronous enumerable of <see cref="SocketMessage"/>.</returns>
        public async IAsyncEnumerable<SocketMessage> GetMessageStream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_isChannelStarted)
            {
                _isChannelStarted = true;
                Logger.LogDebug("Message stream requested, starting channel reader.");
            }

            while (await _messageChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_messageChannel.Reader.TryRead(out var message))
                {
                    yield return message;
                }
            }
        }

        public void Close()
        {
            Logger.LogInformation("Closing MulticastSocket...");
            if (_options.LocalIP == null)
            {
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            }
            _messageChannel.Writer.TryComplete();
#if NET8_0_OR_GREATER
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _receiveCts = null;
#endif
            try
            {
                _udpSocket?.Close();
            }
            catch { }
        }

        /// <summary>
        /// Sends a string asynchronously over the multicast socket.
        /// </summary>
        /// <param name="sendData">The string data to send.</param>
        /// <returns>A task that completes when the send operation is finished.</returns>
        public Task SendAsync(string sendData)
        {
            if (_udpSocket == null)
                return Task.CompletedTask;

            Logger.LogTrace("Sending string data: {Length} characters", sendData.Length);
            byte[] bytesToSend = Encoding.UTF8.GetBytes(sendData);
            return SendAsync(bytesToSend);
        }

        /// <summary>
        /// Sends a byte array asynchronously over the multicast socket.
        /// </summary>
        /// <param name="bytesToSend">The byte array to send.</param>
        /// <returns>A task that completes when the send operation is finished.</returns>
        public Task SendAsync(byte[] bytesToSend)
        {
            return SendAsync(bytesToSend, 0, bytesToSend.Length);
        }

        /// <summary>
        /// Sends a range of bytes asynchronously over the multicast socket.
        /// </summary>
        /// <param name="buffer">The buffer containing the data to send.</param>
        /// <param name="offset">The offset in the buffer where the data starts.</param>
        /// <param name="count">The number of bytes to send.</param>
        /// <returns>A task that completes when the send operation is finished.</returns>
        public Task SendAsync(byte[] buffer, int offset, int count)
        {
            if (_udpSocket == null)
                return Task.CompletedTask;

            Logger.LogTrace("Sending byte data: {Length} bytes", count);
            var remoteEndPoint = new IPEndPoint(IPAddress.Parse(_options.GroupAddress), _options.Port);
            return Task.Factory.FromAsync(
                _udpSocket.BeginSendTo(buffer, offset, count, SocketFlags.None, remoteEndPoint, null, null),
                _udpSocket.EndSendTo);
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
