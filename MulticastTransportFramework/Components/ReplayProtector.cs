using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.MulticastTransportFramework.Components
{
    public class ReplayProtector
    {
        private readonly ConcurrentDictionary<Guid, ReplayWindow> _replayProtection = new ConcurrentDictionary<Guid, ReplayWindow>();
        private ILogger _logger;

        public ILogger Logger { get => _logger; set => _logger = value ?? NullLogger.Instance; }

        public TimeSpan ReplayWindowDuration { get; set; } = TimeSpan.FromSeconds(5);

        public ReplayProtector(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Checks if the message is valid (not a replay and within time window).
        /// Returns true if valid, false if it should be dropped.
        /// </summary>
        public bool IsValid(TransportMessage message, out string reason)
        {
            reason = string.Empty;

            // 1. Timestamp Check
            if (DateTime.TryParse(message.TimeStamp, out var ts))
            {
                var now = DateTime.UtcNow;
                if (ts < now.Subtract(ReplayWindowDuration))
                {
                    reason = $"Message too old (Timestamp: {message.TimeStamp}). Window is {ReplayWindowDuration.TotalSeconds}s.";
                    _logger.LogWarning("Dropped replay/old message {0}. {1}", message.MessageId, reason);
                    return false;
                }
            }

            // 2. Window Initialization / Retrieval
            var window = _replayProtection.GetOrAdd(message.MessageSource.ResourceId, _ => new ReplayWindow());

            // 3. Sequence ID Check
            int senderSequenceNumber = message.SenderSequenceNumber;
            // Legacy check for -1 removed. All messages must have a sequence number now.

            if (!window.CheckAndMark(senderSequenceNumber))
            {
                reason = $"Duplicate or out-of-window sequence ID {senderSequenceNumber}.";
                if (_logger.IsEnabled(LogLevel.Trace))
                    _logger.LogTrace("Replay/Duplicate detected for Seq {0} from {1}", senderSequenceNumber, message.MessageSource.ResourceId);
                return false;
            }

            return true;
        }

        public bool CheckAckRateLimit(Guid sourceId)
        {
            if (_replayProtection.TryGetValue(sourceId, out var window))
            {
                return window.CheckAckRateLimit();
            }
            return true;
        }

        public void Cleanup()
        {
            var threshold = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5));
            foreach (var kvp in _replayProtection)
            {
                if (kvp.Value.LastActivity < threshold)
                {
                    _replayProtection.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
