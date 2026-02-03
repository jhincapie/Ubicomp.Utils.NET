#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.Muticast.TestApp
{
    /// <summary>
    /// Mock message content for testing purposes.
    /// </summary>
    public class MockMessage
    {
        /// <summary>Gets or sets the mock message text.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
