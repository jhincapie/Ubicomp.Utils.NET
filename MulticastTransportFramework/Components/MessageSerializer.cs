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

        public void SerializeToWriter(IBufferWriter<byte> writer, TransportMessage message, int sequenceId, SecurityHandler security)
        {
            var keySession = security.CurrentSession;
            if (security.EncryptionEnabled && keySession != null)
            {
                message.IsEncrypted = true;
            }

            BinaryPacket.SerializeToWriter(writer, message, sequenceId, null,
                 message.IsEncrypted ? (EncryptorDelegate?)keySession!.Encrypt : null,
                 _jsonOptions);
        }

        public TransportMessage? Deserialize(SocketMessage msg, SecurityHandler security, out int senderSequenceId)
        {
            senderSequenceId = -1;
            TransportMessage? tMessage = null;

            if (msg.Length > 0 && msg.Data[0] == BinaryPacket.MagicByte)
            {
                // Binary Protocol
                if (BinaryPacket.TryReadHeader(msg.Data.AsSpan(0, msg.Length), out var h))
                {
                    senderSequenceId = h.SequenceId;
                }

                try
                {
                    var current = security.CurrentSession;
                    var previous = security.PreviousSession;

                    try
                    {
                         tMessage = BinaryPacket.Deserialize(
                            msg.Data.AsSpan(0, msg.Length),
                            msg.ArrivalSequenceId,
                            _jsonOptions,
                            current != null ? (DecryptorDelegate?)current.Decrypt : null);
                    }
                    catch (CryptographicException)
                    {
                        if (previous != null)
                        {
                            tMessage = BinaryPacket.Deserialize(
                                msg.Data.AsSpan(0, msg.Length),
                                msg.ArrivalSequenceId,
                                _jsonOptions,
                                (DecryptorDelegate?)previous.Decrypt);
                        }
                        else throw;
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
                // Legacy JSON
                string sMessage = Encoding.UTF8.GetString(msg.Data, 0, msg.Length);
                tMessage = JsonSerializer.Deserialize<TransportMessage>(sMessage, _jsonOptions);
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

        public string ComputeSignature(TransportMessage message, byte[]? keyBytes)
        {
            var writer = new ArrayBufferWriter<byte>();

            // 1. MessageId
            WriteGuid(writer, message.MessageId);

            // 2. TimeStamp
            WriteAscii(writer, message.TimeStamp);

            // 3. MessageType
            WriteAscii(writer, message.MessageType);

            // 4. RequestAck
            WriteBool(writer, message.RequestAck);

            // 5. IsEncrypted
            WriteBool(writer, message.IsEncrypted);

            // 6. Nonce
            if (message.Nonce != null)
                WriteAscii(writer, message.Nonce);

            // 7. Tag
            if (message.Tag != null)
                WriteAscii(writer, message.Tag);

            // 8. MessageData (JSON)
            using (var jsonWriter = new Utf8JsonWriter(writer))
            {
                JsonSerializer.Serialize(jsonWriter, message.MessageData, _jsonOptions);
            }

            // Hashing
            byte[] hash;
            var payload = writer.WrittenSpan;

            if (keyBytes != null && keyBytes.Length > 0)
            {
                hash = HMACSHA256.HashData(keyBytes, payload);
            }
            else
            {
                hash = SHA256.HashData(payload);
            }

            return Convert.ToBase64String(hash);
        }

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
