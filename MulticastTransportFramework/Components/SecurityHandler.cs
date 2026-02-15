using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.MulticastTransportFramework.Components
{
    public class SecurityHandler : IDisposable
    {
        private readonly KeyManager _keyManager;
        private ILogger _logger;
        private string? _securityKey;

        public bool EncryptionEnabled
        {
            get; set;
        }

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

        public KeyManager KeyManager => _keyManager;

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
        // Legacy Decrypt (string-based) REMOVED

        public void Dispose()
        {
            _keyManager.Dispose();
        }
    }
}
