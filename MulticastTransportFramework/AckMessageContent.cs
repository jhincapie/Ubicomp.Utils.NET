#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Content for an acknowledgement message.
    /// </summary>
    public class AckMessageContent
    {
        /// <summary>
        /// Gets or sets the original message identifier being acknowledged.
        /// </summary>
        public Guid OriginalMessageId
        {
            get; set;
        }
    }
}
