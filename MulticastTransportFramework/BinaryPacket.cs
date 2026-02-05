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
        }

        public static byte[] Serialize(TransportMessage message, int sequenceId, byte[]? nonce, byte[]? tag, byte[] payload)
        {
            // Calculate sizes
            string type = message.MessageType;
            int typeLen = Encoding.UTF8.GetByteCount(type);
            if (typeLen > 255) typeLen = 255;

            string name = message.MessageSource.ResourceName ?? "Unknown";
            int nameLen = Encoding.UTF8.GetByteCount(name);
            if (nameLen > 255) nameLen = 255;

            int nonceLen = nonce?.Length ?? 0;
            int tagLen = tag?.Length ?? 0;

            int headerSize = 1 + 1 + 1 + 1 + 1 + 1 + 10 + 4 + 16 + 16 + 8 + 1 + typeLen + nameLen;
            int cryptoOverhead = nonceLen + tagLen;

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

            // 2. Crypto & Metadata Lengths
            span[3] = (byte)nonceLen;
            span[4] = (byte)tagLen;
            span[5] = (byte)nameLen;

            // Reserved (10 bytes) 6-15

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
            
            // Source Name (K)
            Encoding.UTF8.GetBytes(name, 0, name.Length, packet, 61 + typeLen);

            int offset = 61 + typeLen + nameLen;

            // Crypto Headers
            if (message.IsEncrypted)
            {
                if (nonce != null)
                {
                    nonce.CopyTo(span.Slice(offset, nonceLen));
                    offset += nonceLen;
                }
                if (tag != null)
                {
                    tag.CopyTo(span.Slice(offset, tagLen));
                    offset += tagLen;
                }
            }

            // Payload
            payload.CopyTo(span.Slice(offset));

            return packet;
        }

        public static TransportMessage? Deserialize(ReadOnlySpan<byte> buffer, int sequenceId, JsonSerializerOptions jsonOptions)
        {
             if (buffer.Length < 61) return null;

             if (buffer[0] != MagicByte) return null;

             PacketFlags flags = (PacketFlags)buffer[2];
             
             int nonceLen = buffer[3];
             int tagLen = buffer[4];
             int nameLen = buffer[5];

             int packetSeqId = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(16));

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

             string? nonce = null;
             string? tag = null;
             bool isEnc = flags.HasFlag(PacketFlags.Encrypted);

             if (isEnc)
             {
                 if (nonceLen > 0 && offset + nonceLen <= buffer.Length)
                 {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                     nonce = Convert.ToBase64String(buffer.Slice(offset, nonceLen));
#else
                     nonce = Convert.ToBase64String(buffer.Slice(offset, nonceLen).ToArray());
#endif
                     offset += nonceLen;
                 }

                 if (tagLen > 0 && offset + tagLen <= buffer.Length)
                 {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                     tag = Convert.ToBase64String(buffer.Slice(offset, tagLen));
#else
                     tag = Convert.ToBase64String(buffer.Slice(offset, tagLen).ToArray());
#endif
                     offset += tagLen;
                 }
            }

             var payloadSlice = buffer.Slice(offset);

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
                 messageData = JsonSerializer.Deserialize<JsonElement>(payloadSlice, jsonOptions);
             }

             return new TransportMessage
             {
                 MessageId = msgId,
                 MessageSource = new EventSource(sourceId, sourceName),
                 MessageType = messageType,
                 RequestAck = flags.HasFlag(PacketFlags.RequestAck),
                 IsEncrypted = isEnc,
                 Nonce = nonce,
                 Tag = tag,
                 TimeStamp = new DateTime(ticks).ToString(TransportMessage.DATE_FORMAT_NOW),
                 MessageData = messageData
             };
        }
    }
}