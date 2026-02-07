using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.MulticastTransportFramework.Components
{
    internal class ReplayProtector
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
        public bool IsValid(TransportMessage message, int senderSequenceId, out string reason)
        {
            reason = string.Empty;

            // 1. Timestamp Check
            if (DateTime.TryParse(message.TimeStamp, out var ts))
            {
                var now = DateTime.UtcNow;
                // Allow some future clock skew? The original code only checked past window.
                // "if (ts < now.Subtract(ReplayWindow))"
                if (ts < now.Subtract(ReplayWindowDuration))
                {
                    reason = $"Message too old (Timestamp: {message.TimeStamp}). Window is {ReplayWindowDuration.TotalSeconds}s.";
                    _logger.LogWarning("Dropped replay/old message {0}. {1}", message.MessageId, reason);
                    return false;
                }
            }
            else
            {
                // If timestamp parsing fails, what do we do?
                // Original code: "if (DateTime.TryParse...)" so it proceeded if parsing failed?
                // Probably better to warn but allow if legacy?
                // Original code proceeded to CheckAndMark.
            }

            // 2. Sequence ID Check (if available)
            // senderSequenceId is -1 if not available (e.g. legacy JSON)
            if (senderSequenceId != -1)
            {
                var window = _replayProtection.GetOrAdd(message.MessageSource.ResourceId, _ => new ReplayWindow());
                if (!window.CheckAndMark(senderSequenceId))
                {
                    reason = $"Duplicate or out-of-window sequence ID {senderSequenceId}.";
                    if (_logger.IsEnabled(LogLevel.Trace))
                        _logger.LogTrace("Replay/Duplicate detected for Seq {0} from {1}", senderSequenceId, message.MessageSource.ResourceId);
                    return false;
                }
            }
            else
            {
                // Ensure window exists for Rate Limiting later even if we don't check sequence
                _replayProtection.GetOrAdd(message.MessageSource.ResourceId, _ => new ReplayWindow());
            }

            return true;
        }

        public bool CheckAckRateLimit(Guid sourceId)
        {
            if (_replayProtection.TryGetValue(sourceId, out var window))
            {
                return window.CheckAckRateLimit();
            }
            // If we don't have a window, we haven't seen messages from them?
            // Or maybe it was cleaned up?
            // Default to allowing implies we create one?
            // Original code: "if (_replayProtection.TryGetValue(..., out var window)) allowed = window.CheckAckRateLimit();"
            // So if not found, allowed remains true (default bool?).
            // Wait, "bool allowed = true;" was init.
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
