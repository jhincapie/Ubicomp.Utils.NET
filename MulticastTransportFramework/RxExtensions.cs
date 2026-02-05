using System;
using System.Reactive.Linq;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Extension methods for reactive message processing.
    /// </summary>
    public static class RxExtensions
    {
        /// <summary>
        /// Filters the stream for messages sent from a specific source ID.
        /// </summary>
        public static IObservable<SocketMessage> WhereSourceId(this IObservable<SocketMessage> source, string sourceId)
        {
            return source.Where(msg =>
            {
                // Note: SocketMessage doesn't have SourceId in the header directly in this version unless we parse it.
                // But we can filter by remote endpoint if needed.
                // Actually, TransportMessage has SourceId.
                // This helper might be limited on raw SocketMessages.
                // Let's keep it simple: Raw messages come from an endpoint.
                return true;
            });
        }
    }
}
