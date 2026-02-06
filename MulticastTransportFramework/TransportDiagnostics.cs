#nullable enable
using System;
using System.Text;
using System.Text.Json;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Diagnostic helper for inspecting transport packets.
    /// </summary>
    public static class TransportDiagnostics
    {
        public static string DumpPacket(byte[] packet)
        {
            if (packet == null || packet.Length == 0) return "{ \"error\": \"Empty packet\" }";

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.Append("  \"length\": ").Append(packet.Length).AppendLine(",");

            if (packet[0] == BinaryPacket.MagicByte)
            {
                sb.AppendLine("  \"format\": \"BINARY\",");
                try
                {
                    // Attempt partial parse strictly for diagnostics
                    var span = new ReadOnlySpan<byte>(packet);
                    int offset = 1;

                    // Version (1 byte)
                    byte version = span[offset++];
                    sb.Append("  \"version\": ").Append(version).AppendLine(",");

                    // Flags (1 byte)
                    byte flags = span[offset++];
                    sb.Append("  \"flags\": \"").Append(Convert.ToString(flags, 2).PadLeft(8, '0')).AppendLine("\",");
                    sb.Append("  \"isEncrypted\": ").Append((flags & 1) != 0 ? "true" : "false").AppendLine(",");

                    // SequenceId (4 bytes)
                    int seqId = BitConverter.ToInt32(packet, offset);
                    sb.Append("  \"sequenceId\": ").Append(seqId).AppendLine(",");
                    offset += 4;

                    // TODO: Extract other fields if needed, but payload might be encrypted
                    sb.AppendLine("  \"payloadStatus\": \"(Omitted)\"");

                }
                catch (Exception ex)
                {
                    sb.Append("  \"parseError\": \"").Append(ex.Message).AppendLine("\"");
                }
            }
            else
            {
                sb.AppendLine("  \"format\": \"JSON\",");
                try
                {
                    string json = Encoding.UTF8.GetString(packet);
                     // Validate JSON
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                         sb.AppendLine("  \"validJson\": true,");
                         sb.Append("  \"preview\": \"").Append(json.Substring(0, Math.Min(json.Length, 100)).Replace("\"", "\\\"")).AppendLine("...\"");
                    }
                }
                catch
                {
                    sb.AppendLine("  \"validJson\": false,");
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
