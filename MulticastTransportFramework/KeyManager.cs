using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Represents a cryptographic session with derived keys.
    /// Manages the lifecycle of Integrity and Encryption keys.
    /// </summary>
    public class KeySession : IDisposable
    {
        public SecureMemory IntegrityKey { get; }
        public SecureMemory EncryptionKey { get; }

        // Cache AesGcm instance for performance (thread-safe)
        public AesGcm? AesGcmInstance { get; }

        public KeySession(string masterKey)
        {
            // Derive keys using HKDF logic (HMAC-based for compatibility)
            byte[] masterBytes = Encoding.UTF8.GetBytes(masterKey);
            try
            {
                using (var hmac = new HMACSHA256(masterBytes))
                {
                    // Derive Integrity Key
                    byte[] infoIntegrity = Encoding.UTF8.GetBytes("Ubicomp.Utils.NET.Integrity");
                    byte[] integrityBytes = hmac.ComputeHash(infoIntegrity);
                    IntegrityKey = new SecureMemory(integrityBytes);

                    // Clear intermediate buffers
                    Array.Clear(integrityBytes, 0, integrityBytes.Length);

                    // Derive Encryption Key
                    byte[] infoEncryption = Encoding.UTF8.GetBytes("Ubicomp.Utils.NET.Encryption");
                    byte[] encryptionBytes = hmac.ComputeHash(infoEncryption);
                    EncryptionKey = new SecureMemory(encryptionBytes);

                    Array.Clear(encryptionBytes, 0, encryptionBytes.Length);
                }
            }
            finally
            {
                Array.Clear(masterBytes, 0, masterBytes.Length);
            }

            // Init AesGcm
            // Init AesGcm
            AesGcmInstance = new AesGcm(EncryptionKey.Memory.Span, 16); // 16-byte tag
        }

        public void Dispose()
        {
            IntegrityKey.Dispose();
            EncryptionKey.Dispose();
        }

        public byte[] Decrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> cipherText)
        {
            if (AesGcmInstance != null)
            {
                byte[] plainBytes = new byte[cipherText.Length];
                AesGcmInstance.Decrypt(nonce, cipherText, tag, plainBytes);
                return plainBytes;
            }
            throw new PlatformNotSupportedException("AES-GCM is not supported on this platform.");
        }

        public void Encrypt(ReadOnlySpan<byte> plainText, Span<byte> cipherText, Span<byte> nonce, Span<byte> tag)
        {
            if (AesGcmInstance != null)
            {
                 AesGcmInstance.Encrypt(nonce, plainText, cipherText, tag);
                 return;
            }
             throw new PlatformNotSupportedException("AES-GCM is not supported on this platform.");
        }
    }

    /// <summary>
    /// Manages key rotation and grace periods.
    /// </summary>
    public class KeyManager : IDisposable
    {
        private volatile KeySession? _currentSession;
        private volatile KeySession? _previousSession;
        private readonly ILogger _logger;
        private readonly object _lock = new object();

        public KeyManager(ILogger? logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        public bool HasKey => _currentSession != null;

        public KeySession? Current => _currentSession;
        public KeySession? Previous => _previousSession;

        /// <summary>
        /// Sets the initial key or rotates to a new key.
        /// </summary>
        /// <param name="masterKey">The new master key.</param>
        /// <param name="retainPrevious">True to keep the old key as a fallback (grace period).</param>
        public void SetKey(string masterKey, bool retainPrevious = true)
        {
            lock (_lock)
            {
                var newSession = new KeySession(masterKey);

                if (retainPrevious && _currentSession != null)
                {
                    // Rotate: Current -> Previous
                    _previousSession?.Dispose(); // Dispose old previous
                    _previousSession = _currentSession;
                    _logger.LogInformation("Key Rotated. Previous key retained for grace period.");
                }
                else
                {
                    // Hard Replace: Dispose everything
                    _previousSession?.Dispose();
                    _previousSession = null;
                    _currentSession?.Dispose();
                    _logger.LogInformation("Key Set (No grace period or initial).");
                }

                _currentSession = newSession;
            }
        }

        /// <summary>
        /// Clears the previous key (ends grace period).
        /// </summary>
        public void ClearPreviousKey()
        {
             lock (_lock)
            {
                if (_previousSession != null)
                {
                    _previousSession.Dispose();
                    _previousSession = null;
                    _logger.LogInformation("Previous key cleared (Grace period ended).");
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _currentSession?.Dispose();
                _currentSession = null;
                _previousSession?.Dispose();
                _previousSession = null;
            }
        }
    }
}
