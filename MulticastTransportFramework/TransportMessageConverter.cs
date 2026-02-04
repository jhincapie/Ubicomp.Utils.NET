#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Custom JSON converter for <see cref="TransportMessage"/> objects.
    /// Handles polymorphic deserialization of the message data based on the
    /// message type.
    /// </summary>
    public class TransportMessageConverter : JsonConverter
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
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TransportMessage);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object? value,
                                       JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            TransportMessage message = (TransportMessage)value;

            writer.WriteStartObject();
            writer.WritePropertyName("messageId");
            serializer.Serialize(writer, message.MessageId);
            writer.WritePropertyName("messageSource");
            serializer.Serialize(writer, message.MessageSource);
            writer.WritePropertyName("messageType");
            writer.WriteValue(message.MessageType);

            if (message.RequestAck)
            {
                writer.WritePropertyName("requestAck");
                writer.WriteValue(true);
            }

            writer.WritePropertyName("messageData");
            if (message.MessageData != null)
                serializer.Serialize(writer, message.MessageData);
            else
                writer.WriteNull();

            writer.WritePropertyName("timeStamp");
            writer.WriteValue(message.TimeStamp);
            writer.WriteEndObject();
        }

        /// <inheritdoc />
        public override object? ReadJson(JsonReader reader, Type objectType,
                                         object? existingValue,
                                         JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            TransportMessage message = new TransportMessage();

            message.MessageId =
                jo["messageId"]?.ToObject<Guid>(serializer) ?? Guid.Empty;
            message.MessageSource =
                jo["messageSource"]?.ToObject<EventSource>(serializer) ??
                new EventSource();
            message.MessageType = jo["messageType"]?.Value<string>() ?? string.Empty;
            message.RequestAck = jo["requestAck"]?.Value<bool>() ?? false;
            message.TimeStamp =
                jo["timeStamp"]?.Value<string>() ?? string.Empty;

            JToken? dataToken = jo["messageData"];
            if (dataToken == null || dataToken.Type == JTokenType.Null)
                return message;

            if (!string.IsNullOrEmpty(message.MessageType) &&
                _knownTypes.TryGetValue(message.MessageType,
                                       out Type? targetType) &&
                targetType != null)
            {
                // Deserialize directly to the known type
                object? deserialized =
                    dataToken.ToObject(targetType, serializer);
                if (deserialized != null)
                {
                    message.MessageData = deserialized;
                }
                return message;
            }

            try
            {
                // Attempt best effort.
                object? deserialized = dataToken.ToObject(typeof(object),
                                                          serializer);
                if (deserialized != null)
                {
                    message.MessageData = deserialized;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"Error during best-effort deserialization of transport message content: {e.Message}");
            }

            return message;
        }
    }
}
