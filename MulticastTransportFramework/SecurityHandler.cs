using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    internal static class SecurityHandler
    {
        public static string ComputeHash(TransportMessage msg, byte[] key, JsonSerializerOptions options)
        {
            string dataJson;
            if (msg.MessageData is JsonElement element)
            {
                dataJson = element.GetRawText();
            }
            else
            {
                dataJson = JsonSerializer.Serialize(msg.MessageData, options);
            }

            var sb = new StringBuilder();
            sb.Append(msg.MessageId);
            sb.Append('|');
            sb.Append(msg.TimeStamp);
            sb.Append('|');
            sb.Append(msg.MessageType);
            sb.Append('|');
            if (msg.MessageSource != null)
            {
                sb.Append(msg.MessageSource.ResourceId);
            }
            sb.Append('|');
            sb.Append(msg.RequestAck);
            sb.Append('|');
            sb.Append(dataJson);

            using (var hmac = new HMACSHA256(key))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return Convert.ToBase64String(hash);
            }
        }

        public static bool VerifyHash(TransportMessage msg, byte[] key, JsonSerializerOptions options)
        {
            if (string.IsNullOrEmpty(msg.Hash)) return false;

            string computed = ComputeHash(msg, key, options);

            return FixedTimeEquals(msg.Hash, computed);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left.Length != right.Length) return false;

            int diff = 0;
            for (int i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }
            return diff == 0;
        }
    }
}
