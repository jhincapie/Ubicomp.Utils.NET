using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.MulticastTransportFramework.Components
{
    internal class SecurityHandler : IDisposable
    {
        private readonly KeyManager _keyManager;
        private ILogger _logger;
        private string? _securityKey;

        public bool EncryptionEnabled { get; set; }

        public ILogger Logger
        {
            get => _logger;
            set
            {
                _logger = value ?? NullLogger.Instance;
                _keyManager.Logger = _logger;
            }
        }

        public SecurityHandler(ILogger logger)
        {
            _logger = logger;
            _keyManager = new KeyManager(logger);
        }

        public string? SecurityKey
        {
            get => _securityKey;
            set
            {
                _securityKey = value;
                UpdateKey();
            }
        }

        internal KeyManager KeyManager => _keyManager;

        public KeySession? CurrentSession => _keyManager.Current;
        public KeySession? PreviousSession => _keyManager.Previous;

        private void UpdateKey()
        {
            if (string.IsNullOrEmpty(_securityKey))
            {
                _keyManager.Dispose(); // Clear keys
                return;
            }

            try
            {
                _keyManager.SetKey(_securityKey!, retainPrevious: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update keys from SecurityKey.");
                throw;
            }
        }

        public void HandleRekey(string newKey)
        {
             _keyManager.SetKey(newKey, retainPrevious: true);
        }

        public void ClearPreviousKey() => _keyManager.ClearPreviousKey();

        // Helper for Legacy Decryption
        public string Decrypt(string cipherText, string nonce, string? tag)
        {
            var session = _keyManager.Current;
            if (session == null)
                throw new InvalidOperationException("Cannot decrypt without EncryptionKey.");

            if (session.AesGcmInstance != null && tag != null)
            {
                byte[] nonceBytes = Convert.FromBase64String(nonce);
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] tagBytes = Convert.FromBase64String(tag);
                byte[] plainBytes = new byte[cipherBytes.Length];

                session.AesGcmInstance.Decrypt(nonceBytes, cipherBytes, tagBytes, plainBytes);

                return Encoding.UTF8.GetString(plainBytes);
            }

            throw new PlatformNotSupportedException("AES-GCM is required for decryption but not supported on this platform, or Tag was missing.");
        }

        public void Dispose()
        {
            _keyManager.Dispose();
        }
    }
}
