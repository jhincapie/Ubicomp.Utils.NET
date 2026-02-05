using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Represents a discovered peer in the network.
    /// </summary>
    public class RemotePeer
    {
        public string SourceId { get; }
        public string DeviceName { get; }
        public DateTime LastSeen { get; private set; }
        public string? Metadata { get; }

        public RemotePeer(string sourceId, string deviceName, string? metadata)
        {
            SourceId = sourceId;
            DeviceName = deviceName;
            Metadata = metadata;
            LastSeen = DateTime.UtcNow;
        }

        internal void UpdateLastSeen()
        {
            LastSeen = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Thread-safe collection of active peers.
    /// </summary>
    public class PeerTable
    {
        private readonly Dictionary<string, RemotePeer> _peers = new Dictionary<string, RemotePeer>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public event Action<RemotePeer>? OnPeerDiscovered;
        public event Action<RemotePeer>? OnPeerLost;

        public void UpdatePeer(string sourceId, string deviceName, string? metadata)
        {
            RemotePeer? discovered = null;
            _lock.EnterWriteLock();
            try
            {
                if (_peers.TryGetValue(sourceId, out var existingPeer))
                {
                    existingPeer.UpdateLastSeen();
                }
                else
                {
                    discovered = new RemotePeer(sourceId, deviceName, metadata);
                    _peers[sourceId] = discovered;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            if (discovered != null)
            {
                OnPeerDiscovered?.Invoke(discovered);
            }
        }

        public void CleanupStalePeers(TimeSpan timeout)
        {
            var lostPeers = new List<RemotePeer>();
            DateTime cutoff = DateTime.UtcNow - timeout;

            _lock.EnterWriteLock();
            try
            {
                var staleKeys = _peers.Where(kv => kv.Value.LastSeen < cutoff).Select(kv => kv.Key).ToList();
                foreach (var key in staleKeys)
                {
                    if (_peers.TryGetValue(key, out var p))
                    {
                        lostPeers.Add(p);
                        _peers.Remove(key);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            foreach (var p in lostPeers)
            {
                OnPeerLost?.Invoke(p);
            }
        }

        public IEnumerable<RemotePeer> GetActivePeers()
        {
            _lock.EnterReadLock();
            try
            {
                return _peers.Values.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}
