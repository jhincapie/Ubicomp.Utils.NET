using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    public class TransportMessageConverter : JsonConverter
    {
        // Changed to map Int -> Type for simpler deserialization
        public static Dictionary<int, Type> KnownTypes = new Dictionary<int, Type>();

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

            TransportMessage tMessage = (TransportMessage)value;
            
            writer.WriteStartObject();
            writer.WritePropertyName("messageId");
            serializer.Serialize(writer, tMessage.MessageId);
            writer.WritePropertyName("messageSource");
            serializer.Serialize(writer, tMessage.MessageSource);
            writer.WritePropertyName("messageType");
            writer.WriteValue(tMessage.MessageType);
            
            writer.WritePropertyName("messageData");
            // Simply serialize the object. Newtonsoft will handle the concrete type.
            if (tMessage.MessageData != null)
            {
                serializer.Serialize(writer, tMessage.MessageData);
            }
            else
            {
                writer.WriteNull();
            }
            
            writer.WritePropertyName("timeStamp");
            writer.WriteValue(tMessage.TimeStamp);
            writer.WriteEndObject();
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            TransportMessage tMessage = new TransportMessage();

            tMessage.MessageId = jo["messageId"]?.ToObject<Guid>(serializer) ?? Guid.Empty;
            tMessage.MessageSource = jo["messageSource"]?.ToObject<EventSource>(serializer) ?? new EventSource();
            tMessage.MessageType = jo["messageType"]?.Value<int>() ?? 0;
            tMessage.TimeStamp = jo["timeStamp"]?.Value<string>() ?? string.Empty;

            JToken? dataToken = jo["messageData"];
            if (dataToken != null && dataToken.Type != JTokenType.Null)
            {
                if (KnownTypes.TryGetValue(tMessage.MessageType, out Type? targetType) && targetType != null)
                {
                    // Deserialize directly to the known type
                    object? deserialized = dataToken.ToObject(targetType, serializer);
                    if (deserialized is ITransportMessageContent content)
                    {
                        tMessage.MessageData = content;
                    }
                }
                else
                {
                    try 
                    {
                        // Attempt best effort
                        object? deserialized = dataToken.ToObject(typeof(ITransportMessageContent), serializer);
                        if (deserialized is ITransportMessageContent content)
                        {
                            tMessage.MessageData = content;
                        }
                    }
                    catch
                    {
                        // Best effort failed, messageData remains null (or initialized null! so we might want to assign a fallback if strictly required)
                    }
                }
            }

            return tMessage;
        }
    }
}
