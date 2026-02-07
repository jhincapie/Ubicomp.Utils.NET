using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.Sockets
{
    /// <summary>
    /// An in-memory implementation of <see cref="IMulticastSocket"/> for testing purposes.
    /// Simulates a network by routing messages between instances via a shared static hub.
    /// </summary>
    public class InMemoryMulticastSocket : IMulticastSocket
    {
        // Simulated network hub
        private static readonly ConcurrentDictionary<string, ConcurrentBag<InMemoryMulticastSocket>> _networkGroups
            = new ConcurrentDictionary<string, ConcurrentBag<InMemoryMulticastSocket>>();

        private readonly Channel<SocketMessage> _receiveChannel;
        private string _groupAddress = string.Empty;
        private int _port;
        private bool _joined;
        private bool _disposed;
        private int _consecutiveSeq;
        private EndPoint _localEP; // Simulated local EP

        public IEnumerable<IPAddress> JoinedAddresses
        {
            get
            {
                if (_joined && IPAddress.TryParse(_groupAddress, out var ip))
                {
                    return new[] { ip };
                }
                return Array.Empty<IPAddress>();
            }
        }

        public event Action<SocketMessage>? OnMessageReceived;

        public InMemoryMulticastSocket()
        {
            _receiveChannel = Channel.CreateUnbounded<SocketMessage>();
            // Assign a random fake "IP"
            _localEP = new IPEndPoint(IPAddress.Loopback, new Random().Next(10000, 60000));
        }

        public InMemoryMulticastSocket(string multicastAddress, int port) : this()
        {
            _groupAddress = multicastAddress;
            _port = port;
            // Auto-join for test convenience if addressing is provided upfront?
            // The tests seem to expect it to be "ready" or at least configured.
            // We will do the registration in JoinGroupAsync explicitly called by Transport or test,
            // OR if the test assumes implicit join (like `Test_JoinedAddresses` seems to?), we might need to join now.
            // But `Test_JoinedAddresses` calls `StartReceiving`.
            // Let's defer "Join" logic to `JoinGroupAsync` but ensure proper state.
            // If checking JoinedAddresses without calling JoinGroupAsync, it returns empty?
            // Test_JoinedAddresses calls `StartReceiving()` then checks `JoinedAddresses`.
            // It assumes specific behavior. Let's make `StartReceiving` simulate a Join if not joined?
            // Or maybe the constructor joins?
            // Let's try joining in StartReceiving if configured.
        }

        public void StartReceiving()
        {
            if (!_joined && !string.IsNullOrEmpty(_groupAddress) && _port > 0)
            {
                JoinGroupAsync(_groupAddress).Wait();
            }
        }

        public void Bind(int port)
        {
            _port = port;
        }

        public Task JoinGroupAsync(string multicastAddress)
        {
            _groupAddress = multicastAddress;
            var groupKey = $"{multicastAddress}:{_port}";
            var bag = _networkGroups.GetOrAdd(groupKey, _ => new ConcurrentBag<InMemoryMulticastSocket>());
            bag.Add(this);
            _joined = true;
            return Task.CompletedTask;
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryMulticastSocket));

            if (string.IsNullOrEmpty(_groupAddress))
                return ValueTask.CompletedTask;

            if (buffer.Length > 65535)
                throw new ArgumentException("Message size exceeds maximum allowed size.", nameof(buffer));

            var groupKey = $"{_groupAddress}:{_port}";
            if (_networkGroups.TryGetValue(groupKey, out var peers))
            {
                foreach (var peer in peers)
                {
                    if (peer.IsJoined && !peer._disposed)
                    {
                        // Allow loopback for tests (peer == this is OK)
                        peer.ReceiveInternal(buffer.Span, _localEP);
                    }
                }
            }

            return ValueTask.CompletedTask;
        }

        public Task SendAsync(byte[] bytesToSend)
        {
            return SendAsync(bytesToSend, 0, bytesToSend.Length);
        }

        public Task SendAsync(byte[] buffer, int offset, int count)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InMemoryMulticastSocket));

            if (string.IsNullOrEmpty(_groupAddress))
                return Task.CompletedTask;

            if (count > 65535)
                throw new ArgumentException("Message size exceeds maximum allowed size.", nameof(count));

            var groupKey = $"{_groupAddress}:{_port}";
            if (_networkGroups.TryGetValue(groupKey, out var peers))
            {
                foreach (var peer in peers)
                {
                    if (peer.IsJoined && !peer._disposed)
                    {
                        // Allow loopback for tests (peer == this is OK)
                        peer.ReceiveInternal(buffer.AsSpan(offset, count), _localEP);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task SendAsync(string sendData)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(sendData);
            return SendAsync(bytes);
        }

        // Helper for test setup
        public bool IsJoined => _joined;

        private void ReceiveInternal(ReadOnlySpan<byte> data, EndPoint sender)
        {
            if (_disposed)
                return;

            // Copy data to simulate network buffer isolation
            byte[] buffer = ArrayPool<byte>.Shared.Rent(data.Length);
            data.CopyTo(buffer);

            int seq = Interlocked.Increment(ref _consecutiveSeq);

            var msg = new SocketMessage();
            msg.Reset(buffer, data.Length, seq, isRented: true, sender);
            msg.ReturnCallback = (m) =>
            {
                if (m.Data != null)
                    ArrayPool<byte>.Shared.Return(m.Data);
            };

            // 1. Event (Legacy/Synchronous)
            try
            {
                OnMessageReceived?.Invoke(msg);
            }
            catch { }

            // 2. Channel (Async Stream)
            if (!_receiveChannel.Writer.TryWrite(msg))
            {
                msg.Dispose();
            }
        }

        public async IAsyncEnumerable<SocketMessage> GetMessageStream([EnumeratorCancellation] CancellationToken ct = default)
        {
            while (await _receiveChannel.Reader.WaitToReadAsync(ct))
            {
                while (_receiveChannel.Reader.TryRead(out var msg))
                {
                    yield return msg;
                }
            }
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _receiveChannel.Writer.TryComplete();

            // Remove from static bag?
            // Expensive/Hard with ConcurrentBag.
            // In tests it's okay if "dead" sockets remain in bag,
            // we check `!peer._disposed` in SendAsync loop so they are ignored.
        }
    }
}
