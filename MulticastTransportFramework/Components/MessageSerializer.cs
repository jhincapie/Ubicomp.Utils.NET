using System;
using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.Sockets;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.MulticastTransportFramework.Components
{
    internal class MessageSerializer
    {
        private readonly JsonSerializerOptions _jsonOptions;
        private ILogger _logger;

        public ILogger Logger { get => _logger; set => _logger = value ?? NullLogger.Instance; }

        public MessageSerializer(ILogger logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        public JsonSerializerOptions JsonOptions => _jsonOptions;

        public void SerializeToWriter(IBufferWriter<byte> writer, TransportMessage message, SecurityHandler security)
        {
            var keySession = security.CurrentSession;
            if (security.EncryptionEnabled && keySession != null)
            {
                message.IsEncrypted = true;
            }

            // Pass integrity key (HMAC) if not encrypted but key is available, or if encrypted (though GCM handles it)
            // BinaryPacket now handles choosing between GCM Tag or HMAC Tag based on IsEncrypted.
            // If IsEncrypted is true, we pass the Encryptor. If false, we pass the integrity key.
            // Actually, BinaryPacket.SerializeToWriter takes both.
            // We should pass the integrity key from the CurrentSession if available.

            byte[]? integrityKey = keySession?.IntegrityKey.Memory.ToArray();

            try
            {
                BinaryPacket.SerializeToWriter(writer, message, integrityKey,
                     message.IsEncrypted ? (EncryptorDelegate?)keySession!.Encrypt : null,
                     _jsonOptions);
            }
            finally
            {
                // Clean up sensitive key copy if possible, though ToArray() created a new copy on heap that GC handles.
                // Ideally we'd use Spans throughout but BinaryPacket takes byte[] for integrityKey for now.
                if (integrityKey != null) Array.Clear(integrityKey, 0, integrityKey.Length);
            }
        }

        public TransportMessage? Deserialize(SocketMessage msg, SecurityHandler security)
        {
            TransportMessage? tMessage = null;

            if (msg.Length > 0 && msg.Data[0] == BinaryPacket.MagicByte)
            {
                // Binary Protocol
                try
                {
                    var current = security.CurrentSession;
                    var previous = security.PreviousSession;

                    // Helper to convert Memory<byte> to byte[] temporarily for the API
                    // In a future refactor, BinaryPacket should take ReadOnlySpan<byte> for key.
                    byte[]? currentIntKey = current?.IntegrityKey.Memory.ToArray();
                    byte[]? previousIntKey = previous?.IntegrityKey.Memory.ToArray();

                    try
                    {
                        try
                        {
                             tMessage = BinaryPacket.Deserialize(
                                msg.Data.AsSpan(0, msg.Length),
                                _jsonOptions,
                                current != null ? (DecryptorDelegate?)current.Decrypt : null,
                                currentIntKey);
                        }
                        catch (System.Security.Authentication.AuthenticationException)
                        {
                            // Try previous key if available
                            if (previous != null)
                            {
                                tMessage = BinaryPacket.Deserialize(
                                    msg.Data.AsSpan(0, msg.Length),
                                    _jsonOptions,
                                    (DecryptorDelegate?)previous.Decrypt,
                                    previousIntKey);
                            }
                            else throw;
                        }
                    }
                    finally
                    {
                        if (currentIntKey != null) Array.Clear(currentIntKey, 0, currentIntKey.Length);
                        if (previousIntKey != null) Array.Clear(previousIntKey, 0, previousIntKey.Length);
                    }
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Failed to deserialize binary packet.");
                     throw; // Propagate for handling/logging upstack (OnMessageError)
                }
            }
            else
            {
                // Legacy JSON - REMOVED
                // We no longer support legacy JSON fallback.
                _logger.LogWarning("Received non-binary packet (Legacy JSON). Dropping.");
                return null;
            }

            return tMessage;
        }

        public void DeserializeContent(ref TransportMessage tMessage, Type targetType)
        {
            if (tMessage.MessageData is JsonElement element)
            {
                 tMessage.MessageData = JsonSerializer.Deserialize(element, targetType, _jsonOptions)!;
            }
            else if (tMessage.MessageData is string jsonString)
            {
                 tMessage.MessageData = JsonSerializer.Deserialize(jsonString, targetType, _jsonOptions)!;
            }
        }

        // ComputeSignature REMOVED


        private static readonly byte[] TrueBytes = Encoding.UTF8.GetBytes("true");
        private static readonly byte[] FalseBytes = Encoding.UTF8.GetBytes("false");

        private void WriteBool(IBufferWriter<byte> writer, bool value)
        {
            var bytes = value ? TrueBytes : FalseBytes;
            writer.Write(bytes);
        }

        private void WriteGuid(IBufferWriter<byte> writer, Guid value)
        {
            // Guid "D" format is 36 chars
            var span = writer.GetSpan(36);
            if (Utf8Formatter.TryFormat(value, span, out int bytesWritten, new StandardFormat('D')))
            {
                writer.Advance(bytesWritten);
            }
            else
            {
                // Fallback
                WriteAscii(writer, value.ToString());
            }
        }

        private void WriteAscii(IBufferWriter<byte> writer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            // Bolt: Zero-allocation optimization
            int byteCount = Encoding.UTF8.GetByteCount(value);
            var span = writer.GetSpan(byteCount);
            int written = Encoding.UTF8.GetBytes(value, span);
            writer.Advance(written);
        }
    }
}
