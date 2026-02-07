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
            public readonly int SequenceId;
            public readonly long Ticks;
            public readonly string MessageType; // We might want Span<char> but string is required by GateKeeper for now

            public PacketHeader(int seqId, long ticks, string type)
            {
                SequenceId = seqId;
                Ticks = ticks;
                MessageType = type;
            }
        }

        public enum PacketFlags : byte
        {
            None = 0,
            RequestAck = 1 << 0,
            Encrypted = 1 << 1,
        }

        public static TransportMessage? Deserialize(ReadOnlySpan<byte> buffer, int sequenceId, JsonSerializerOptions jsonOptions, DecryptorDelegate? decryptor)
        {
             if (buffer.Length < 61) return null;

             if (buffer[0] != MagicByte) return null;

             PacketFlags flags = (PacketFlags)buffer[2];

             int nonceLen = buffer[3];
             int tagLen = buffer[4];
             int nameLen = buffer[5];

             Guid msgId;
             Guid sourceId;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
             msgId = new Guid(buffer.Slice(20, 16));
             sourceId = new Guid(buffer.Slice(36, 16));
#else
             msgId = new Guid(buffer.Slice(20, 16).ToArray());
             sourceId = new Guid(buffer.Slice(36, 16).ToArray());
#endif
             long ticks = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(52));

             int typeLen = buffer[60];
             if (buffer.Length < 61 + typeLen + nameLen) return null;

             string messageType;
             string sourceName;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
             messageType = Encoding.UTF8.GetString(buffer.Slice(61, typeLen));
             sourceName = Encoding.UTF8.GetString(buffer.Slice(61 + typeLen, nameLen));
#else
             messageType = Encoding.UTF8.GetString(buffer.Slice(61, typeLen).ToArray());
             sourceName = Encoding.UTF8.GetString(buffer.Slice(61 + typeLen, nameLen).ToArray());
#endif
             int offset = 61 + typeLen + nameLen;

             bool isEnc = flags.HasFlag(PacketFlags.Encrypted);

             object? messageData = null;

             if (isEnc)
             {
                 if (decryptor == null) throw new System.Security.Authentication.AuthenticationException("Received encrypted packet but no decryptor configured.");

                 // Bounds check
                 if (offset + nonceLen + tagLen > buffer.Length) return null;

                 var nonceSpan = buffer.Slice(offset, nonceLen);
                 var tagSpan = buffer.Slice(offset + nonceLen, tagLen);
                 var cipherSpan = buffer.Slice(offset + nonceLen + tagLen);

                 byte[] plainBytes = decryptor(nonceSpan, tagSpan, cipherSpan);

                 // Payload is JSON bytes
                 messageData = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(plainBytes, jsonOptions);
             }
             else
             {
                 var payloadSlice = buffer.Slice(offset);
                 messageData = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(payloadSlice, jsonOptions);
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
                 TimeStamp = new DateTime(ticks).ToString(TransportMessage.DATE_FORMAT_NOW),
                 MessageData = messageData!
             };
        }

        public static void SerializeToWriter(IBufferWriter<byte> writer, TransportMessage message, int sequenceId, byte[]? integrityKey, EncryptorDelegate? encryptor, JsonSerializerOptions? jsonOptions = null)
        {
            // Calculate sizes
            string type = message.MessageType;
            int typeLen = Encoding.UTF8.GetByteCount(type);
            if (typeLen > 255) typeLen = 255;

            string name = message.MessageSource.ResourceName ?? "Unknown";
            int nameLen = Encoding.UTF8.GetByteCount(name);
            if (nameLen > 255) nameLen = 255;

            // Prepare Payload
            byte[] payloadBytes;
            if (message.MessageData is IBinarySerializable binarySerializable)
            {
                 var tempWriter = new ArrayBufferWriter<byte>();
                 binarySerializable.Write(tempWriter);
                 payloadBytes = tempWriter.WrittenSpan.ToArray();
            }
            else if (message.MessageData is byte[] b) payloadBytes = b;
            else if (message.MessageData is string s) payloadBytes = Encoding.UTF8.GetBytes(s);
            else payloadBytes = JsonSerializer.SerializeToUtf8Bytes(message.MessageData, jsonOptions);

            bool isEncrypted = encryptor != null && message.IsEncrypted;
            int nonceLen = isEncrypted ? 12 : 0; // GCM Nonce
            int tagLen = isEncrypted ? 16 : 0;   // GCM Tag

            // Header: 61 bytes fixed + variable strings
            int headerFixedSize = 61;
            int headerTotalSize = headerFixedSize + typeLen + nameLen;

            // Allocate Span for Header
            Span<byte> headerSpan = writer.GetSpan(headerTotalSize);

            // 1. Magic & Version & Flags
            headerSpan[0] = MagicByte;
            headerSpan[1] = ProtocolVersion;

            PacketFlags flags = PacketFlags.None;
            if (message.RequestAck) flags |= PacketFlags.RequestAck;
            if (isEncrypted) flags |= PacketFlags.Encrypted;
            headerSpan[2] = (byte)flags;

            // 2. Metadata Lengths
            headerSpan[3] = (byte)nonceLen;
            headerSpan[4] = (byte)tagLen;
            headerSpan[5] = (byte)nameLen;

            // Reserved (10 bytes) 6-15 (Clear it)
            headerSpan.Slice(6, 10).Clear();

            // SeqId (4)
            BinaryPrimitives.WriteInt32LittleEndian(headerSpan.Slice(16), sequenceId);

            // MsgId (16)
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            message.MessageId.TryWriteBytes(headerSpan.Slice(20));
            message.MessageSource.ResourceId.TryWriteBytes(headerSpan.Slice(36));
#else
            message.MessageId.ToByteArray().CopyTo(headerSpan.Slice(20));
            message.MessageSource.ResourceId.ToByteArray().CopyTo(headerSpan.Slice(36));
#endif

            // Timestamp (8) - Ticks
            long ticks = DateTime.TryParse(message.TimeStamp, out var dt) ? dt.Ticks : DateTime.Now.Ticks;
            BinaryPrimitives.WriteInt64LittleEndian(headerSpan.Slice(52), ticks);

            // TypeLen (1)
            headerSpan[60] = (byte)typeLen;

            // Type (N)
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER || NET8_0_OR_GREATER
            Encoding.UTF8.GetBytes(type, headerSpan.Slice(61, typeLen));
            Encoding.UTF8.GetBytes(name, headerSpan.Slice(61 + typeLen, nameLen));
#else
            var typeBytes = Encoding.UTF8.GetBytes(type);
            typeBytes.CopyTo(headerSpan.Slice(61, typeLen));
            var nameBytes = Encoding.UTF8.GetBytes(name);
            nameBytes.CopyTo(headerSpan.Slice(61 + typeLen, nameLen));
#endif

            writer.Advance(headerTotalSize);

            // Payload & Crypto
            if (isEncrypted && encryptor != null)
            {
                // Native GCM Encryption
                int totalCryptoSize = nonceLen + tagLen + payloadBytes.Length;
                Span<byte> cryptoSpan = writer.GetSpan(totalCryptoSize);

                // Split spans
                Span<byte> nonceSpan = cryptoSpan.Slice(0, nonceLen);
                Span<byte> tagSpan = cryptoSpan.Slice(nonceLen, tagLen);
                Span<byte> cipherSpan = cryptoSpan.Slice(nonceLen + tagLen, payloadBytes.Length);

                // Generate Nonce
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                System.Security.Cryptography.RandomNumberGenerator.Fill(nonceSpan);
#else
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    var tempNonce = new byte[nonceLen];
                    rng.GetBytes(tempNonce);
                    tempNonce.CopyTo(nonceSpan);
                }
#endif

                // Encrypt directly into output span
                encryptor(payloadBytes, cipherSpan, nonceSpan, tagSpan);

                writer.Advance(totalCryptoSize);
            }
            else
            {
                // Plaintext Payload
                writer.Write(payloadBytes);
            }
        }

        public static void SerializeToWriter(IBufferWriter<byte> writer, TransportMessage message, int sequenceId, byte[]? integrityKey, byte[]? encryptionKey, JsonSerializerOptions? jsonOptions = null)
        {
            // Calculate sizes
            string type = message.MessageType;
            int typeLen = Encoding.UTF8.GetByteCount(type);
            if (typeLen > 255) typeLen = 255;

            string name = message.MessageSource.ResourceName ?? "Unknown";
            int nameLen = Encoding.UTF8.GetByteCount(name);
            if (nameLen > 255) nameLen = 255;

            // Prepare Payload (Serialize to JSON first if not already bytes/string)
            // Ideally we want to write JSON directly to the writer too, but for now we might have intermediate bytes for the payload
            // unless we use a fresh writer for payload.
            // optimization: TransportMessage.MessageData might already be byte[] or string.

            byte[] payloadBytes;
            if (message.MessageData is IBinarySerializable binarySerializable)
            {
                 // P5: IBinarySerializable Optimization
                 // TODO: Write directly to writer. For now we serialize to temp buffer because our layout assumes payload at end?
                 // Actually layout assumes header then payload.
                 // But we have logic below that calculates header size based on TypeLen/NameLen etc.
                 // We can start writing header, then write payload.

                 // For now, to minimize refactor risk in this step, let's treat it as byte[]
                 var tempWriter = new ArrayBufferWriter<byte>();
                 binarySerializable.Write(tempWriter);
                 payloadBytes = tempWriter.WrittenSpan.ToArray();
            }
            else if (message.MessageData is byte[] b) payloadBytes = b;
            else if (message.MessageData is string s) payloadBytes = Encoding.UTF8.GetBytes(s);
            else payloadBytes = JsonSerializer.SerializeToUtf8Bytes(message.MessageData, jsonOptions);

            bool isEncrypted = encryptionKey != null && message.IsEncrypted;
            int nonceLen = isEncrypted ? 12 : 0; // GCM Nonce
            int tagLen = isEncrypted ? 16 : 0;   // GCM Tag

            // Header: 61 bytes fixed + variable strings
            // [Magic:1][Version:1][Flags:1][NonceLen:1][TagLen:1][NameLen:1][Reserved:10][SeqId:4][MsgId:16][SourceId:16][Tick:8][TypeLen:1]
            // + [Type:N] + [Name:K]

            int headerFixedSize = 61;
            int headerTotalSize = headerFixedSize + typeLen + nameLen;

            // Allocate Span for Header
            Span<byte> headerSpan = writer.GetSpan(headerTotalSize);

            // 1. Magic & Version & Flags
            headerSpan[0] = MagicByte;
            headerSpan[1] = ProtocolVersion;

            PacketFlags flags = PacketFlags.None;
            if (message.RequestAck) flags |= PacketFlags.RequestAck;
            if (isEncrypted) flags |= PacketFlags.Encrypted;
            headerSpan[2] = (byte)flags;

            // 2. Metadata Lengths
            headerSpan[3] = (byte)nonceLen;
            headerSpan[4] = (byte)tagLen;
            headerSpan[5] = (byte)nameLen;

            // Reserved (10 bytes) 6-15 (Clear it)
            headerSpan.Slice(6, 10).Clear();

            // SeqId (4)
            BinaryPrimitives.WriteInt32LittleEndian(headerSpan.Slice(16), sequenceId);

            // MsgId (16)
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            message.MessageId.TryWriteBytes(headerSpan.Slice(20));
            message.MessageSource.ResourceId.TryWriteBytes(headerSpan.Slice(36));
#else
            message.MessageId.ToByteArray().CopyTo(headerSpan.Slice(20));
            message.MessageSource.ResourceId.ToByteArray().CopyTo(headerSpan.Slice(36));
#endif

            // Timestamp (8) - Ticks
            long ticks = DateTime.TryParse(message.TimeStamp, out var dt) ? dt.Ticks : DateTime.Now.Ticks;
            BinaryPrimitives.WriteInt64LittleEndian(headerSpan.Slice(52), ticks);

            // TypeLen (1)
            headerSpan[60] = (byte)typeLen;

            // Type (N)
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER || NET8_0_OR_GREATER
            Encoding.UTF8.GetBytes(type, headerSpan.Slice(61, typeLen));

            // Source Name (K)
            Encoding.UTF8.GetBytes(name, headerSpan.Slice(61 + typeLen, nameLen));
#else
            var typeBytes = Encoding.UTF8.GetBytes(type);
            typeBytes.CopyTo(headerSpan.Slice(61, typeLen));
            var nameBytes = Encoding.UTF8.GetBytes(name);
            nameBytes.CopyTo(headerSpan.Slice(61 + typeLen, nameLen));
#endif

            writer.Advance(headerTotalSize);

            // Payload & Crypto
            if (isEncrypted && encryptionKey != null)
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                // Native GCM Encryption
                // Req: Nonce (12), Tag (16), Ciphertext (PayloadLength)
                int totalCryptoSize = nonceLen + tagLen + payloadBytes.Length;
                Span<byte> cryptoSpan = writer.GetSpan(totalCryptoSize);

                // Split spans
                Span<byte> nonceSpan = cryptoSpan.Slice(0, nonceLen);
                Span<byte> tagSpan = cryptoSpan.Slice(nonceLen, tagLen);
                Span<byte> cipherSpan = cryptoSpan.Slice(nonceLen + tagLen, payloadBytes.Length);

                // Generate Nonce
                System.Security.Cryptography.RandomNumberGenerator.Fill(nonceSpan);

                // Encrypt directly into output span
#if NET8_0_OR_GREATER
                 using (var aesGcm = new System.Security.Cryptography.AesGcm(encryptionKey, 16))
#else
                 using (var aesGcm = new System.Security.Cryptography.AesGcm(encryptionKey))
#endif
                {
                    aesGcm.Encrypt(nonceSpan, payloadBytes, cipherSpan, tagSpan);
                }

                writer.Advance(totalCryptoSize);
#else
                throw new PlatformNotSupportedException("Native AES-GCM requires .NET Standard 2.1 or .NET Core 3.0+");
#endif
            }
            else
            {
                // Plaintext Payload
                writer.Write(payloadBytes);
            }

            // Future helper: Integrity Signature (HMAC) could be appended here if we wanted to sign the whole packet
            // Currently the signature is inside the payload or header?
            // In the original code, Signature was part of JSON or checked separately.
            // For BinaryPacket, we might want to append a signature footer if IntegrityKey is present.
            // But the current spec doesn't show a footer in "Wire Format" comment,
            // relying on GCM Tag for integrity (if encrypted) or external mechanism.
            // We will stick to the format: Header + [Crypto] + Payload.
        }

        // Keep legacy Deserialize for now, but mark Obsolete or update it.
        // We need an updated Reader-based Deserialize too.
        public static TransportMessage? Deserialize(ReadOnlySpan<byte> buffer, int sequenceId, JsonSerializerOptions jsonOptions, byte[]? encryptionKey)
        {
             if (buffer.Length < 61) return null;

             if (buffer[0] != MagicByte) return null;

             PacketFlags flags = (PacketFlags)buffer[2];

             int nonceLen = buffer[3];
             int tagLen = buffer[4];
             int nameLen = buffer[5];

             // int packetSeqId = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(16));

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
             Guid msgId = new Guid(buffer.Slice(20, 16));
             Guid sourceId = new Guid(buffer.Slice(36, 16));
#else
             Guid msgId = new Guid(buffer.Slice(20, 16).ToArray());
             Guid sourceId = new Guid(buffer.Slice(36, 16).ToArray());
#endif
             long ticks = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(52));

             int typeLen = buffer[60];
             if (buffer.Length < 61 + typeLen + nameLen) return null;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
             string messageType = Encoding.UTF8.GetString(buffer.Slice(61, typeLen));
             string sourceName = Encoding.UTF8.GetString(buffer.Slice(61 + typeLen, nameLen));
#else
             string messageType = Encoding.UTF8.GetString(buffer.Slice(61, typeLen).ToArray());
             string sourceName = Encoding.UTF8.GetString(buffer.Slice(61 + typeLen, nameLen).ToArray());
#endif
             int offset = 61 + typeLen + nameLen;

             bool isEnc = flags.HasFlag(PacketFlags.Encrypted);

             // Extract Payload (Auth/Decryption handled here for native Binary Packet)
             object? messageData = null;

             if (isEnc)
             {
                 if (encryptionKey == null) throw new System.Security.Authentication.AuthenticationException("Received encrypted packet but no key configured.");

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                 // Bounds check
                 if (offset + nonceLen + tagLen > buffer.Length) return null;

                 var nonceSpan = buffer.Slice(offset, nonceLen);
                 var tagSpan = buffer.Slice(offset + nonceLen, tagLen);
                 var cipherSpan = buffer.Slice(offset + nonceLen + tagLen);

                 // Decrypt into temporary buffer (or string)
                 // We don't know the plaintext length exactly but it matches ciphertext length for GCM
                 byte[] plainBytes = new byte[cipherSpan.Length];

#if NET8_0_OR_GREATER
                 using (var aesGcm = new System.Security.Cryptography.AesGcm(encryptionKey, 16))
#else
                 using (var aesGcm = new System.Security.Cryptography.AesGcm(encryptionKey))
#endif
                 {
                     aesGcm.Decrypt(nonceSpan, cipherSpan, tagSpan, plainBytes);
                 }

                 // Payload is JSON bytes
                 messageData = JsonSerializer.Deserialize<JsonElement>(plainBytes, jsonOptions);
#else
                 throw new PlatformNotSupportedException("Native AES-GCM requires .NET Standard 2.1 or .NET Core 3.0+");
#endif
             }
             else
             {
                 var payloadSlice = buffer.Slice(offset);
                 messageData = JsonSerializer.Deserialize<JsonElement>(payloadSlice, jsonOptions);
             }

             return new TransportMessage
             {
                 MessageId = msgId,
                 MessageSource = new EventSource(sourceId, sourceName),
                 MessageType = messageType,
                 RequestAck = flags.HasFlag(PacketFlags.RequestAck),
                 IsEncrypted = isEnc,
                 Nonce = null, // Not needed for app layer anymore
                 Tag = null,
                 TimeStamp = new DateTime(ticks).ToString(TransportMessage.DATE_FORMAT_NOW),
                 MessageData = messageData!
             };
        }
        public static bool TryReadHeader(ReadOnlySpan<byte> buffer, out PacketHeader header)
        {
            header = default;
            if (buffer.Length < 61) return false;
            if (buffer[0] != MagicByte) return false;

            // SeqId (4) at offset 16
            int seqId = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(16));

            // Tick (8) at offset 52
            long ticks = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(52));

            // TypeLen at 60
            int typeLen = buffer[60];
            int nameLen = buffer[5]; // NameLen at 5

            if (buffer.Length < 61 + typeLen + nameLen) return false;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            string messageType = Encoding.UTF8.GetString(buffer.Slice(61, typeLen));
#else
            string messageType = Encoding.UTF8.GetString(buffer.Slice(61, typeLen).ToArray());
#endif
            header = new PacketHeader(seqId, ticks, messageType);
            return true;
        }
    }
}
