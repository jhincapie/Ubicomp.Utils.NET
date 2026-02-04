#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    public class TransportMessageConverter : JsonConverter<TransportMessage>
    {
        private readonly IDictionary<int, Type> _knownTypes;

        public TransportMessageConverter(IDictionary<int, Type> knownTypes)
        {
            _knownTypes = knownTypes;
        }

        public override TransportMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            TransportMessage message = new TransportMessage();

            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("messageId", out JsonElement idElement))
                {
                    if (idElement.TryGetGuid(out Guid guid))
                        message.MessageId = guid;
                }

                if (root.TryGetProperty("messageSource", out JsonElement sourceElement))
                {
                    if (sourceElement.ValueKind != JsonValueKind.Null)
                    {
                        message.MessageSource = JsonSerializer.Deserialize<EventSource>(sourceElement.GetRawText(), options)!;
                    }
                }

                // Ensure MessageSource is not null (matching original behavior/constructor)
                if (message.MessageSource == null)
                {
                    message.MessageSource = new EventSource();
                }

                if (root.TryGetProperty("messageType", out JsonElement typeElement))
                {
                    if (typeElement.TryGetInt32(out int typeVal))
                        message.MessageType = typeVal;
                }

                if (root.TryGetProperty("requestAck", out JsonElement ackElement))
                {
                     if (ackElement.ValueKind == JsonValueKind.True || ackElement.ValueKind == JsonValueKind.False)
                        message.RequestAck = ackElement.GetBoolean();
                }

                if (root.TryGetProperty("timeStamp", out JsonElement timeElement))
                {
                    message.TimeStamp = timeElement.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("messageData", out JsonElement dataElement))
                {
                    if (dataElement.ValueKind != JsonValueKind.Null)
                    {
                        if (_knownTypes.TryGetValue(message.MessageType, out Type? targetType) && targetType != null)
                        {
                            message.MessageData = JsonSerializer.Deserialize(dataElement.GetRawText(), targetType, options)!;
                        }
                        else
                        {
                            // Best effort
                             message.MessageData = JsonSerializer.Deserialize<JsonElement>(dataElement.GetRawText(), options);
                        }
                    }
                }
            }

            return message;
        }

        public override void Write(Utf8JsonWriter writer, TransportMessage value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // Correctly write the GUID as a string property value
            writer.WriteString("messageId", value.MessageId.ToString());

            writer.WritePropertyName("messageSource");
            JsonSerializer.Serialize(writer, value.MessageSource, options);

            writer.WriteNumber("messageType", value.MessageType);

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
