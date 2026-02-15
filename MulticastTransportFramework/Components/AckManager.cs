using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.MulticastTransportFramework.Components
{
    public class AckManager
    {
        private readonly ConcurrentDictionary<Guid, AckSession> _activeSessions = new ConcurrentDictionary<Guid, AckSession>();
        private ILogger _logger;

        public ILogger Logger
        {
            get => _logger; set => _logger = value ?? NullLogger.Instance;
        }

        public TimeSpan DefaultAckTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public bool AutoSendAcks { get; set; } = false;

        public AckManager(ILogger logger)
        {
            _logger = logger;
        }

        public AckSession CreateSession(Guid messageId, TimeSpan? timeout, bool track = true)
        {
            var session = new AckSession(messageId);

            if (track)
            {
                _activeSessions.TryAdd(messageId, session);

                // Auto-cleanup after timeout or completion
                _ = session.WaitAsync(timeout ?? DefaultAckTimeout).ContinueWith(t =>
                {
                    _activeSessions.TryRemove(messageId, out _);
                });
            }

            return session;
        }

        public void ProcessIncomingAck(Guid originalMessageId, EventSource source)
        {
            if (_activeSessions.TryGetValue(originalMessageId, out var session))
            {
                _logger.LogTrace("Received Ack for message {0} from {1}", originalMessageId, source.ResourceName);
                session.ReportAck(source);
            }
        }

        public bool ShouldAutoSendAck(TransportMessage message, ReplayProtector replayProtector)
        {
            if (!AutoSendAcks)
                return false;
            if (!message.RequestAck)
                return false;
            if (message.MessageType == TransportComponent.AckMessageType)
                return false;

            if (replayProtector.CheckAckRateLimit(message.MessageSource.ResourceId))
            {
                return true;
            }

            _logger.LogWarning("Ack Rate Limit exceeded for {0}. Dropping Ack.", message.MessageSource.ResourceId);
            return false;
        }
    }
}
