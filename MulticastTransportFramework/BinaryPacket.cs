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
    /// [Magic:1][Version:1][Flags:1][Reserved:13][SeqId:4][MsgId:16][SourceId:16][Tick:8][TypeLen:1][Type:N][Nonce:12?][Tag:16?][Payload:M]
    /// </summary>
    internal static class BinaryPacket
    {
        public const byte MagicByte = 0xAA;
        public const byte ProtocolVersion = 1;

        [Flags]
        public enum PacketFlags : byte
        {
            None = 0,
            RequestAck = 1 << 0,
            Encrypted = 1 << 1,
            // Future flags
        }

        public static byte[] Serialize(TransportMessage message, int sequenceId, byte[]? nonce, byte[]? tag, byte[] payload)
        {
            // Calculate size
            string type = message.MessageType;
            int typeLen = Encoding.UTF8.GetByteCount(type);
            if (typeLen > 255) typeLen = 255; // Cap type length

            int headerSize = 1 + 1 + 1 + 13 + 4 + 16 + 16 + 8 + 1 + typeLen;
            int cryptoOverhead = 0;
            if (message.IsEncrypted)
            {
                cryptoOverhead += (nonce?.Length ?? 0);
                cryptoOverhead += (tag?.Length ?? 0);
            }

            int totalSize = headerSize + cryptoOverhead + payload.Length;
            byte[] packet = new byte[totalSize];
            var span = packet.AsSpan();

            // 1. Magic & Version & Flags
            span[0] = MagicByte;
            span[1] = ProtocolVersion;

            PacketFlags flags = PacketFlags.None;
            if (message.RequestAck) flags |= PacketFlags.RequestAck;
            if (message.IsEncrypted) flags |= PacketFlags.Encrypted;
            span[2] = (byte)flags;

            // Reserved (13 bytes) - Zeroed by default (new byte[])

            // SeqId (4)
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(16), sequenceId);

            // MsgId (16)
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            message.MessageId.TryWriteBytes(span.Slice(20));
#else
            Array.Copy(message.MessageId.ToByteArray(), 0, packet, 20, 16);
#endif

            // SourceId (16)
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            message.MessageSource.ResourceId.TryWriteBytes(span.Slice(36));
#else
            Array.Copy(message.MessageSource.ResourceId.ToByteArray(), 0, packet, 36, 16);
#endif

            // Timestamp (8) - Ticks
            DateTime.TryParse(message.TimeStamp, out var dt);
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(52), dt.Ticks);

            // TypeLen (1)
            span[60] = (byte)typeLen;

            // Type (N)
            Encoding.UTF8.GetBytes(type, 0, type.Length, packet, 61);

            int offset = 61 + typeLen;

            // Crypto Headers
            if (message.IsEncrypted)
            {
                if (nonce != null)
                {
                    nonce.CopyTo(span.Slice(offset, nonce.Length));
                    offset += nonce.Length;
                }
                if (tag != null)
                {
                    tag.CopyTo(span.Slice(offset, tag.Length));
                    offset += tag.Length;
                }
            }

            // Payload
            payload.CopyTo(span.Slice(offset));

            return packet;
        }

        public static TransportMessage? Deserialize(ReadOnlySpan<byte> buffer, int sequenceId, JsonSerializerOptions jsonOptions)
        {
             if (buffer.Length < 61) return null; // Too small

             // Check Magic
             if (buffer[0] != MagicByte) return null;

             // Version check (optional, for now accept 1)
             if (buffer[1] != ProtocolVersion)
             {
                 // Handle version mismatch if needed.
             }

             PacketFlags flags = (PacketFlags)buffer[2];

             // Reserved: 3-15

             // SeqId Check? The transport layer handles seqId checking, but we receive it in header.
             int packetSeqId = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(16));
             // We can ignore packetSeqId if we trust the loop, OR we can use it.
             // The incoming 'sequenceId' arg is from the socket layer (arrival order).
             // The packetSeqId is from the sender.

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
             Guid msgId = new Guid(buffer.Slice(20, 16));
             Guid sourceId = new Guid(buffer.Slice(36, 16));
#else
             Guid msgId = new Guid(buffer.Slice(20, 16).ToArray());
             Guid sourceId = new Guid(buffer.Slice(36, 16).ToArray());
#endif
             long ticks = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(52));

             int typeLen = buffer[60];
             if (buffer.Length < 61 + typeLen) return null;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
             string messageType = Encoding.UTF8.GetString(buffer.Slice(61, typeLen));
#else
             string messageType = Encoding.UTF8.GetString(buffer.Slice(61, typeLen).ToArray());
#endif
             int offset = 61 + typeLen;

             string? nonce = null;
             string? tag = null;
             bool isEnc = flags.HasFlag(PacketFlags.Encrypted);

             if (isEnc)
             {
                 // GCM Nonce (12) + Tag (16) = 28 bytes
                 // OR CBC Nonce (16) + Tag (0)

                 // How to distinguish?
                 // Let's assume standard GCM sizes: Nonce 12, Tag 16.

                 // If we are strictly GCM:
                 if (offset + 12 <= buffer.Length)
                 {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                     nonce = Convert.ToBase64String(buffer.Slice(offset, 12));
#else
                     nonce = Convert.ToBase64String(buffer.Slice(offset, 12).ToArray());
#endif
                     offset += 12;
                 }

                 if (offset + 16 <= buffer.Length)
                 {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                     tag = Convert.ToBase64String(buffer.Slice(offset, 16));
#else
                     tag = Convert.ToBase64String(buffer.Slice(offset, 16).ToArray());
#endif
                     offset += 16;
                 }
            }

             var payloadSlice = buffer.Slice(offset);

             // Payload is either Encrypted Bytes OR JSON Bytes.
             // If Encrypted: MessageData = CipherText (Base64 String).
             // If Not: MessageData = JSON Object (Deserialized).

             object messageData;
             if (isEnc)
             {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                 messageData = Convert.ToBase64String(payloadSlice);
#else
                 messageData = Convert.ToBase64String(payloadSlice.ToArray());
#endif
             }
             else
             {
                 // Deserialize JSON payload
                 var utf8Reader = new Utf8JsonReader(payloadSlice);
                 // We need target type. We don't have it here easily without knownTypes.
                 // So we assume JsonElement/Document for specific type logic later.
                 // Or we return a "Raw" TransportMessage and let Component handle it.
                 // JsonSerializer.Deserialize<object>(...) will return JsonElement.
                 messageData = JsonSerializer.Deserialize<JsonElement>(payloadSlice, jsonOptions);
             }

             return new TransportMessage
             {
                 MessageId = msgId,
                 MessageSource = new EventSource(sourceId, "Unknown"), // We don't put Name in binary header to save space. Lookup?
                 MessageType = messageType,
                 RequestAck = flags.HasFlag(PacketFlags.RequestAck),
                 IsEncrypted = isEnc,
                 Nonce = nonce,
                 Tag = tag,
                 TimeStamp = new DateTime(ticks).ToString(TransportMessage.DATE_FORMAT_NOW), // Conversion for compat
                 MessageData = messageData
             };
        }
    }
}
