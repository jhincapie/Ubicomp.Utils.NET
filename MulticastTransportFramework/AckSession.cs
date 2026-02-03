#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Represents an active tracking session for acknowledgements of a specific message.
    /// </summary>
    public class AckSession
    {
        private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
        private readonly ConcurrentBag<EventSource> _receivedAcks = new ConcurrentBag<EventSource>();

        /// <summary>
        /// Gets the original message identifier being tracked.
        /// </summary>
        public Guid OriginalMessageId
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether at least one acknowledgement has been received.
        /// </summary>
        public bool IsAnyAckReceived => !_receivedAcks.IsEmpty;

        /// <summary>
        /// Gets the collection of responders who have acknowledged the message.
        /// </summary>
        public IEnumerable<EventSource> ReceivedAcks => _receivedAcks;

        /// <summary>
        /// Fired immediately whenever a valid acknowledgement for this message arrives.
        /// </summary>
        public event Action<AckSession, EventSource>? OnAckReceived;

        /// <summary>
        /// Initializes a new instance of the <see cref="AckSession"/> class.
        /// </summary>
        /// <param name="originalMessageId">The unique identifier of the message to track.</param>
        public AckSession(Guid originalMessageId)
        {
            OriginalMessageId = originalMessageId;
        }

        /// <summary>
        /// Reports an acknowledgement from a source.
        /// </summary>
        /// <param name="source">The source that acknowledged the message.</param>
        public void ReportAck(EventSource source)
        {
            _receivedAcks.Add(source);
            OnAckReceived?.Invoke(this, source);
            _tcs.TrySetResult(true);
        }

        /// <summary>
        /// Awaits for acknowledgements until the specified timeout.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>True if at least one acknowledgement was received, otherwise false.</returns>
        public async Task<bool> WaitAsync(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            using (cts.Token.Register(() => _tcs.TrySetResult(IsAnyAckReceived)))
            {
                return await _tcs.Task;
            }
        }
    }
}
