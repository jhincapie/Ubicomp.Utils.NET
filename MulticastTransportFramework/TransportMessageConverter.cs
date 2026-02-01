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

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
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

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            TransportMessage tMessage = new TransportMessage();

            tMessage.MessageId = jo["messageId"].ToObject<Guid>(serializer);
            tMessage.MessageSource = jo["messageSource"].ToObject<EventSource>(serializer);
            tMessage.MessageType = jo["messageType"].Value<int>();
            tMessage.TimeStamp = jo["timeStamp"].Value<string>();

            JToken dataToken = jo["messageData"];
            if (dataToken != null && dataToken.Type != JTokenType.Null)
            {
                if (KnownTypes.TryGetValue(tMessage.MessageType, out Type targetType))
                {
                    // Deserialize directly to the known type
                    tMessage.MessageData = (ITransportMessageContent)dataToken.ToObject(targetType, serializer);
                }
                else
                {
                    // Fallback: leave as null or try to deserialize to interface (which usually results in null or error if abstract)
                    // For now, we leave it null or let the user handle raw JToken if they inspected it differently, 
                    // but TransportMessage expects ITransportMessageContent.
                    // We could try to deserialize to a generic object/dictionary if needed, but the contract says ITransportMessageContent.
                    try 
                    {
                        // Attempt best effort, though likely to fail for interfaces without type info
                        tMessage.MessageData = (ITransportMessageContent)dataToken.ToObject(typeof(ITransportMessageContent), serializer);
                    }
                    catch
                    {
                        tMessage.MessageData = null; 
                    }
                }
            }

            return tMessage;
        }
    }
}