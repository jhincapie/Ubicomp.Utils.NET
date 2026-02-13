using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Handles binary serialization for the transport protocol to avoid JSON overhead.
    /// Wire Format:
    /// [Magic:1][Version:1][Flags:1][NonceLen:1][TagLen:1][NameLen:1][Reserved:10][SeqId:4][MsgId:16][SourceId:16][Tick:8][TypeLen:1][Type:N][Name:K][Nonce:L][Tag:M][Payload:P]
    /// </summary>
    public interface IBinarySerializable
    {
        void Write(IBufferWriter<byte> writer);
    }

    public delegate byte[] DecryptorDelegate(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> cipherText);
    public delegate void EncryptorDelegate(ReadOnlySpan<byte> plainText, Span<byte> cipherText, Span<byte> nonce, Span<byte> tag);

    internal static class BinaryPacket
    {
        public const byte MagicByte = 0xAA;
        public const byte ProtocolVersion = 1;

        public readonly ref struct PacketHeader
        {
            public readonly int SenderSequenceNumber;
            public readonly long Ticks;
            public readonly string MessageType; // We might want Span<char> but string is required by GateKeeper for now
            public readonly Guid SourceId;

            public PacketHeader(int senderSequenceNumber, long ticks, string type, Guid sourceId)
            {
                SenderSequenceNumber = senderSequenceNumber;
                Ticks = ticks;
                MessageType = type;
                SourceId = sourceId;
            }
        }

        public enum PacketFlags : byte
        {
            None = 0,
            RequestAck = 1 << 0,
            Encrypted = 1 << 1,
        }

        public static TransportMessage? Deserialize(ReadOnlySpan<byte> buffer, JsonSerializerOptions jsonOptions, DecryptorDelegate? decryptor, byte[]? integrityKey)
        {
            if (buffer.Length < 61)
                return null;

            if (buffer[0] != MagicByte)
                return null;

            PacketFlags flags = (PacketFlags)buffer[2];

            int nonceLen = buffer[3];
            int tagLen = buffer[4];
            int nameLen = buffer[5];

            Guid msgId;
            Guid sourceId;
            // SeqId (4) at offset 16
            int senderSequenceNumber = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(16));
            msgId = new Guid(buffer.Slice(20, 16));
            sourceId = new Guid(buffer.Slice(36, 16));
            long ticks = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(52));

            int typeLen = buffer[60];
            if (buffer.Length < 61 + typeLen + nameLen)
                return null;

            string messageType;
            string sourceName;
            messageType = Encoding.UTF8.GetString(buffer.Slice(61, typeLen));
            sourceName = Encoding.UTF8.GetString(buffer.Slice(61 + typeLen, nameLen));
            int offset = 61 + typeLen + nameLen;

            bool isEnc = flags.HasFlag(PacketFlags.Encrypted);

            object? messageData = null;

            if (isEnc)
            {
                if (decryptor == null)
                    throw new System.Security.Authentication.AuthenticationException("Received encrypted packet but no decryptor configured.");

                // Bounds check
                if (offset + nonceLen + tagLen > buffer.Length)
                    return null;

                var nonceSpan = buffer.Slice(offset, nonceLen);
                var tagSpan = buffer.Slice(offset + nonceLen, tagLen);
                var cipherSpan = buffer.Slice(offset + nonceLen + tagLen);

                byte[] plainBytes = decryptor(nonceSpan, tagSpan, cipherSpan);

                // Payload is JSON bytes
                messageData = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(plainBytes, jsonOptions);
            }
            else
            {
                // Integrity check for unencrypted messages
                if (integrityKey != null && integrityKey.Length > 0 && tagLen > 0)
                {
                    if (offset + tagLen > buffer.Length)
                        return null;

                    var tagSpan = buffer.Slice(offset, tagLen);
                    var payloadSpan = buffer.Slice(offset + tagLen);

                    // Compute HMAC
                    byte[] computedHash = System.Security.Cryptography.HMACSHA256.HashData(integrityKey, payloadSpan);
                    if (!computedHash.AsSpan().SequenceEqual(tagSpan))
                    {
                        throw new System.Security.Authentication.AuthenticationException("Integrity check failed.");
                    }

                    messageData = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(payloadSpan, jsonOptions);
                }
                else
                {
                    // No integrity check or no tag
                    if (offset > buffer.Length) return null;
                    var payloadSlice = buffer.Slice(offset);
                    messageData = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(payloadSlice, jsonOptions);
                }
            }

            return new TransportMessage
            {
                MessageId = msgId,
                MessageSource = new EventSource(sourceId, sourceName),
                MessageType = messageType,
                RequestAck = flags.HasFlag(PacketFlags.RequestAck),
                IsEncrypted = isEnc,
                Nonce = null,
                Tag = null,
                Ticks = ticks,
                MessageData = messageData!,
                SenderSequenceNumber = senderSequenceNumber
            };
        }

        public static void SerializeToWriter(IBufferWriter<byte> writer, TransportMessage message, byte[]? integrityKey, EncryptorDelegate? encryptor, JsonSerializerOptions? jsonOptions = null)
        {
            // Calculate sizes
            string type = message.MessageType;
            int typeLen = Encoding.UTF8.GetByteCount(type);
            if (typeLen > 255)
                typeLen = 255;

            string name = message.MessageSource.ResourceName ?? "Unknown";
            int nameLen = Encoding.UTF8.GetByteCount(name);
            if (nameLen > 255)
                nameLen = 255;

            bool isEncrypted = encryptor != null && message.IsEncrypted;
            // Native GCM: Nonce=12, Tag=16
            // HMAC-SHA256: Nonce=0, Tag=32
            int nonceLen = isEncrypted ? 12 : 0;
            int tagLen = isEncrypted ? 16 : (integrityKey != null && integrityKey.Length > 0 ? 32 : 0);

            // Header: 61 bytes fixed + variable strings
            int headerFixedSize = 61;
            int headerTotalSize = headerFixedSize + typeLen + nameLen;

            // Allocate Span for Header
            Span<byte> headerSpan = writer.GetSpan(headerTotalSize);

            // 1. Magic & Version & Flags
            headerSpan[0] = MagicByte;
            headerSpan[1] = ProtocolVersion;

            PacketFlags flags = PacketFlags.None;
            if (message.RequestAck)
                flags |= PacketFlags.RequestAck;
            if (isEncrypted)
                flags |= PacketFlags.Encrypted;
            headerSpan[2] = (byte)flags;

            // 2. Metadata Lengths
            headerSpan[3] = (byte)nonceLen;
            headerSpan[4] = (byte)tagLen;
            headerSpan[5] = (byte)nameLen;

            // Reserved (10 bytes) 6-15 (Clear it)
            headerSpan.Slice(6, 10).Clear();

            // SeqId (4)
            BinaryPrimitives.WriteInt32LittleEndian(headerSpan.Slice(16), message.SenderSequenceNumber);

            // MsgId (16)
            message.MessageId.TryWriteBytes(headerSpan.Slice(20));
            message.MessageSource.ResourceId.TryWriteBytes(headerSpan.Slice(36));

            // Timestamp (8) - Ticks
            long ticks = message.Ticks;
            if (ticks == 0) ticks = DateTime.UtcNow.Ticks;
            BinaryPrimitives.WriteInt64LittleEndian(headerSpan.Slice(52), ticks);

            // TypeLen (1)
            headerSpan[60] = (byte)typeLen;

            // Type (N)
            Encoding.UTF8.GetBytes(type, headerSpan.Slice(61, typeLen));
            Encoding.UTF8.GetBytes(name, headerSpan.Slice(61 + typeLen, nameLen));

            writer.Advance(headerTotalSize);

            // Payload & Crypto
            if (isEncrypted && encryptor != null)
            {
                // Prepare Payload for Encryption
                byte[] payloadBytes;
                if (message.MessageData is IBinarySerializable binarySerializable)
                {
                    var tempWriter = new ArrayBufferWriter<byte>();
                    binarySerializable.Write(tempWriter);
                    payloadBytes = tempWriter.WrittenSpan.ToArray();
                }
                else if (message.MessageData is byte[] b)
                    payloadBytes = b;
                else if (message.MessageData is string s)
                    payloadBytes = Encoding.UTF8.GetBytes(s);
                else
                    payloadBytes = JsonSerializer.SerializeToUtf8Bytes(message.MessageData, jsonOptions);

                // Native GCM Encryption
                int totalCryptoSize = nonceLen + tagLen + payloadBytes.Length;
                Span<byte> cryptoSpan = writer.GetSpan(totalCryptoSize);

                // Split spans
                Span<byte> nonceSpan = cryptoSpan.Slice(0, nonceLen);
                Span<byte> tagSpan = cryptoSpan.Slice(nonceLen, tagLen);
                Span<byte> cipherSpan = cryptoSpan.Slice(nonceLen + tagLen, payloadBytes.Length);

                // Generate Nonce
                System.Security.Cryptography.RandomNumberGenerator.Fill(nonceSpan);

                // Encrypt directly into output span
                encryptor(payloadBytes, cipherSpan, nonceSpan, tagSpan);

                writer.Advance(totalCryptoSize);
            }
            else
            {
                // Plaintext Payload
                if (integrityKey != null && integrityKey.Length > 0)
                {
                    // If integrity key is present, we need payload bytes for HMAC
                    byte[] payloadBytes;
                    if (message.MessageData is IBinarySerializable binarySerializable)
                    {
                        var tempWriter = new ArrayBufferWriter<byte>();
                        binarySerializable.Write(tempWriter);
                        payloadBytes = tempWriter.WrittenSpan.ToArray();
                    }
                    else if (message.MessageData is byte[] b)
                        payloadBytes = b;
                    else if (message.MessageData is string s)
                        payloadBytes = Encoding.UTF8.GetBytes(s);
                    else
                        payloadBytes = JsonSerializer.SerializeToUtf8Bytes(message.MessageData, jsonOptions);

                    // Compute HMAC
                    byte[] hash = System.Security.Cryptography.HMACSHA256.HashData(integrityKey, payloadBytes);
                    writer.Write(hash); // 32 bytes
                    writer.Write(payloadBytes);
                }
                else
                {
                    // Zero Allocation Optimization for truly plain messages
                    if (message.MessageData is IBinarySerializable binarySerializable)
                    {
                        binarySerializable.Write(writer);
                    }
                    else if (message.MessageData is byte[] b)
                    {
                        writer.Write(b);
                    }
                    else if (message.MessageData is string s)
                    {
                        int byteCount = Encoding.UTF8.GetByteCount(s);
                        var span = writer.GetSpan(byteCount);
                        int written = Encoding.UTF8.GetBytes(s, span);
                        writer.Advance(written);
                    }
                    else
                    {
                        using (var jsonWriter = new Utf8JsonWriter(writer))
                        {
                            JsonSerializer.Serialize(jsonWriter, message.MessageData, jsonOptions);
                        }
                    }
                }
            }
        }

        public static bool TryReadHeader(ReadOnlySpan<byte> buffer, out PacketHeader header)
        {
            header = default;
            if (buffer.Length < 61)
                return false;
            if (buffer[0] != MagicByte)
                return false;

            // SeqId (4) at offset 16
            int senderSequenceNumber = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(16));

            // SourceId (16) at offset 36
            Guid sourceId = new Guid(buffer.Slice(36, 16));

            // Tick (8) at offset 52
            long ticks = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(52));

            // TypeLen at 60
            int typeLen = buffer[60];
            int nameLen = buffer[5]; // NameLen at 5

            if (buffer.Length < 61 + typeLen + nameLen)
                return false;

            string messageType = Encoding.UTF8.GetString(buffer.Slice(61, typeLen));
            header = new PacketHeader(senderSequenceNumber, ticks, messageType, sourceId);
            return true;
        }
    }
}
