using System;


namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// System message used to distribute a new security key to peers.
    /// This message should be encrypted with the *current* key.
    /// </summary>
    [MessageType("sys.rekey")]
    public class RekeyMessage
    {
        /// <summary>
        /// Gets or sets the new security key (Base64 encoded).
        /// </summary>
        public string NewKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a unique identifier for this key version.
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the time when this key becomes effective (UTC).
        /// If null or past, effective immediately.
        /// </summary>
        public DateTime? EffectiveFrom { get; set; }
    }
}
