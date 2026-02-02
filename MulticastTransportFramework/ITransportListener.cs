#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

    /// <summary>
    /// Defines a listener for messages received through the multicast transport
    /// framework.
    /// </summary>
    public interface ITransportListener
    {
        /// <summary>
        /// Called when a transport message is received.
        /// </summary>
        /// <param name="message">The deserialized transport message.</param>
        /// <param name="rawMessage">The raw string representation of the
        /// message.</param>
        void MessageReceived(TransportMessage message, string rawMessage);
    }

}
