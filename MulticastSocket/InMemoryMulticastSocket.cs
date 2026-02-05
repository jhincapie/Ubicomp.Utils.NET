#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Ubicomp.Utils.NET.Sockets
{
    /// <summary>
    /// An in-memory implementation of <see cref="IMulticastSocket"/> for testing.
    /// Simulates a multicast network using shared channels.
    /// </summary>
    public class InMemoryMulticastSocket : IMulticastSocket
    {
        // Shared bus for all instances joining the same (Group, Port).
        // Key: "$Group:$Port"
        private static readonly ConcurrentDictionary<string, Channel<byte[]>> _networkBus = new();

        private readonly string _groupAddress;
        private readonly int _port;
        private readonly List<IPAddress> _joinedAddresses = new();
        private readonly Channel<SocketMessage> _receiveChannel = Channel.CreateUnbounded<SocketMessage>();
        private CancellationTokenSource? _listeningCts;
        private readonly IPAddress _virtualInterfaceIp;

        public event Action<SocketMessage>? OnMessageReceived;

        public IEnumerable<IPAddress> JoinedAddresses => _joinedAddresses;

        public InMemoryMulticastSocket(string groupAddress, int port)
        {
            _groupAddress = groupAddress;
            _port = port;
            // Assign a random 127.x.x.x helper IP to simulate an interface
            var rnd = new Random();
            _virtualInterfaceIp = IPAddress.Parse($"127.0.{rnd.Next(1, 255)}.{rnd.Next(1, 255)}");
        }

        public void StartReceiving()
        {
            if (_listeningCts != null) return; // Already started

            _listeningCts = new CancellationTokenSource();
            string busKey = $"{_groupAddress}:{_port}";

            // Get or create the shared bus for this group
            var bus = _networkBus.GetOrAdd(busKey, _ => Channel.CreateUnbounded<byte[]>());

            // Start a task to listen to the bus and forward to our receive channel
            _ = Task.Run(async () =>
            {
                var reader = bus.Reader;
                try
                {
                    while (await reader.WaitToReadAsync(_listeningCts.Token))
                    {
                        while (reader.TryRead(out byte[] data))
                        {
                            // Simulate reception
                            // Copy data to simulate network isolation
                            byte[] receivedData = data.ToArray();

                            var msg = new SocketMessage(receivedData, 0); // SeqId 0 for now, or maintain local counter
                            // In real socket, SeqID is per-socket. Here we can leave 0 or increment.

                            OnMessageReceived?.Invoke(msg);
                            _receiveChannel.Writer.TryWrite(msg);
                        }
                    }
                }
                catch (OperationCanceledException) { }
            });

            // "Join" the loopback interface by default
            _joinedAddresses.Add(_virtualInterfaceIp);
        }

        public async Task SendAsync(byte[] bytesToSend)
        {
            string busKey = $"{_groupAddress}:{_port}";
            if (_networkBus.TryGetValue(busKey, out var bus))
            {
                // Clone buffer (simulate wire)
                byte[] wireData = bytesToSend.ToArray();
                await bus.Writer.WriteAsync(wireData);
            }
        }

        public Task SendAsync(byte[] buffer, int offset, int count)
        {
            byte[] slice = new byte[count];
            Array.Copy(buffer, offset, slice, 0, count);
            return SendAsync(slice);
        }

        public Task SendAsync(string sendData)
        {
            return SendAsync(System.Text.Encoding.UTF8.GetBytes(sendData));
        }

        public IAsyncEnumerable<SocketMessage> GetMessageStream(CancellationToken cancellationToken = default)
        {
            return _receiveChannel.Reader.ReadAllAsync(cancellationToken);
        }

        public void Close()
        {
            _listeningCts?.Cancel();
            _receiveChannel.Writer.TryComplete();
        }

        public void Dispose()
        {
            Close();
        }
    }
}
