#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Custom JSON converter for <see cref="TransportMessage"/> objects.
    /// Handles polymorphic deserialization of the message data based on the
    /// message type.
    /// </summary>
    public class TransportMessageConverter : JsonConverter<TransportMessage>
    {
        private readonly IDictionary<string, Type> _knownTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportMessageConverter"/> class.
        /// </summary>
        /// <param name="knownTypes">The dictionary of known message types for deserialization.</param>
        public TransportMessageConverter(IDictionary<string, Type> knownTypes)
        {
            _knownTypes = knownTypes;
        }

        /// <inheritdoc />
        public override TransportMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                var root = doc.RootElement;
                var message = new TransportMessage();

                if (root.TryGetProperty("messageId", out var idProp))
                {
                    message.MessageId = idProp.GetGuid();
                }

                if (root.TryGetProperty("messageSource", out var sourceProp))
                {
                    message.MessageSource = JsonSerializer.Deserialize<EventSource>(sourceProp.GetRawText(), options) ?? new EventSource();
                }

                if (root.TryGetProperty("messageType", out var typeProp))
                {
                    message.MessageType = typeProp.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("requestAck", out var ackProp))
                {
                    message.RequestAck = ackProp.GetBoolean();
                }

                if (root.TryGetProperty("timeStamp", out var timeProp))
                {
                    message.TimeStamp = timeProp.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("messageData", out var dataProp) && dataProp.ValueKind != JsonValueKind.Null)
                {
                    if (!string.IsNullOrEmpty(message.MessageType) && _knownTypes.TryGetValue(message.MessageType, out Type? targetType) && targetType != null)
                    {
                        message.MessageData = JsonSerializer.Deserialize(dataProp.GetRawText(), targetType, options)!;
                    }
                    else
                    {
                        try
                        {
                            // Best effort.
                            message.MessageData = JsonSerializer.Deserialize<object>(dataProp.GetRawText(), options)!;
                        }
                        catch (Exception e)
                        {
                             Console.Error.WriteLine(
                                $"Error during best-effort deserialization of transport message content: {e.Message}");
                        }
                    }
                }

                return message;
            }
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, TransportMessage value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("messageId", value.MessageId);

            writer.WritePropertyName("messageSource");
            JsonSerializer.Serialize(writer, value.MessageSource, options);

            writer.WriteString("messageType", value.MessageType);

            if (value.RequestAck)
            {
                writer.WriteBoolean("requestAck", true);
            }

            writer.WritePropertyName("messageData");
            if (value.MessageData != null)
            {
                JsonSerializer.Serialize(writer, value.MessageData, value.MessageData.GetType(), options);
            }
            else
            {
                writer.WriteNullValue();
            }

            writer.WriteString("timeStamp", value.TimeStamp);

            writer.WriteEndObject();
        }
    }
}
