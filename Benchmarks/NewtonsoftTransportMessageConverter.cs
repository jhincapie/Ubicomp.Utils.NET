using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Benchmarks
{
    public class NewtonsoftTransportMessageConverter : JsonConverter
    {
        private readonly IDictionary<string, Type> _knownTypes;

        public NewtonsoftTransportMessageConverter(IDictionary<string, Type> knownTypes)
        {
            _knownTypes = knownTypes;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TransportMessage);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
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

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            TransportMessage message = new TransportMessage();

            message.MessageId = jo["messageId"]?.ToObject<Guid>(serializer) ?? Guid.Empty;
            message.MessageSource = jo["messageSource"]?.ToObject<EventSource>(serializer) ?? new EventSource();
            message.MessageType = jo["messageType"]?.Value<string>() ?? string.Empty;
            message.RequestAck = jo["requestAck"]?.Value<bool>() ?? false;
            message.TimeStamp = jo["timeStamp"]?.Value<string>() ?? string.Empty;

            JToken? dataToken = jo["messageData"];
            if (dataToken == null || dataToken.Type == JTokenType.Null)
                return message;

            if (!string.IsNullOrEmpty(message.MessageType) &&
                _knownTypes.TryGetValue(message.MessageType, out Type? targetType) &&
                targetType != null)
            {
                object? deserialized = dataToken.ToObject(targetType, serializer);
                if (deserialized != null)
                    message.MessageData = deserialized;
                return message;
            }

            try
            {
                object? deserialized = dataToken.ToObject(typeof(object), serializer);
                if (deserialized != null)
                    message.MessageData = deserialized;
            }
            catch (Exception) { }

            return message;
        }
    }
}
