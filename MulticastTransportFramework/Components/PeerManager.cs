using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.MulticastTransportFramework.Components
{
    public class PeerManager : IDisposable
    {
        private readonly PeerTable _peerTable = new PeerTable();
        private ILogger _logger;

        public ILogger Logger
        {
            get => _logger; set => _logger = value ?? NullLogger.Instance;
        }

        private CancellationTokenSource? _heartbeatCts;
        private Task? _heartbeatTask;

        public TimeSpan? HeartbeatInterval
        {
            get; set;
        }
        public string? InstanceMetadata
        {
            get; set;
        }

        public IEnumerable<RemotePeer> ActivePeers => _peerTable.GetActivePeers();

        public event Action<RemotePeer>? OnPeerDiscovered
        {
            add => _peerTable.OnPeerDiscovered += value;
            remove => _peerTable.OnPeerDiscovered -= value;
        }

        public event Action<RemotePeer>? OnPeerLost
        {
            add => _peerTable.OnPeerLost += value;
            remove => _peerTable.OnPeerLost -= value;
        }

        public event Action? OnHeartbeatTick;

        public PeerManager(ILogger logger)
        {
            _logger = logger;
        }

        public void Start(Func<HeartbeatMessage, Task> sender, string senderSourceId, string senderResourceName)
        {
            Stop();

            if (HeartbeatInterval.HasValue)
            {
                _heartbeatCts = new CancellationTokenSource();
                var token = _heartbeatCts.Token;
                var interval = HeartbeatInterval.Value;

                // Capture locals for task
                var metadata = InstanceMetadata;
                var sourceId = senderSourceId;
                var resourceName = senderResourceName;
                var startTime = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();

                _heartbeatTask = Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var heartbeat = new HeartbeatMessage
                            {
                                SourceId = sourceId,
                                DeviceName = resourceName,
                                UptimeSeconds = (DateTime.UtcNow - startTime).TotalSeconds,
                                Metadata = metadata
                            };

                            await sender(heartbeat);

                            // Trigger cleanup logic
                            OnHeartbeatTick?.Invoke();

                            // Cleanup stale peers
                            // Use 3x interval or some default
                            _peerTable.CleanupStalePeers(TimeSpan.FromSeconds(interval.TotalSeconds * 3));

                            await Task.Delay(interval, token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in Heartbeat Loop");
                        }
                    }
                }, token);

                _logger.LogInformation("Heartbeat enabled with interval {0}", interval);
            }
        }

        public void Stop()
        {
            _heartbeatCts?.Cancel();
            try
            {
                _heartbeatTask?.Wait(1000);
            }
            catch { }
            _heartbeatCts?.Dispose();
            _heartbeatCts = null;
        }

        public void HandleHeartbeat(HeartbeatMessage msg, string localResourceId)
        {
            if (msg.SourceId == localResourceId)
                return; // Ignore self

            _peerTable.UpdatePeer(msg.SourceId, msg.DeviceName, msg.Metadata);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
