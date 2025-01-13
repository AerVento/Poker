using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Framework.Serialization
{
    public class DictionaryConverter<TKey, TValue> : JsonConverter<IDictionary<TKey, TValue>>
    {
        public override IDictionary<TKey, TValue> ReadJson(JsonReader reader, Type objectType, IDictionary<TKey, TValue> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

            foreach (var property in obj.PropertyValues())
            {
                JObject pairObj = property.Value<JObject>();
                TKey key = pairObj.GetValue("key").ToObject<TKey>(serializer);
                TValue value = pairObj.GetValue("value").ToObject<TValue>(serializer);
                dictionary.Add(key, value);
            }

            return dictionary;
        }

        public override void WriteJson(JsonWriter writer, IDictionary<TKey, TValue> value, JsonSerializer serializer)
        {
            JObject obj = new JObject();
            foreach (var pair in value)
            {
                JObject item = new JObject()
                {
                    {"key", JToken.FromObject(pair.Key, serializer)},
                    {"value", JToken.FromObject(pair.Value, serializer)}
                };

                obj.Add($"item({pair.Key}))", item);
            }

            obj.WriteTo(writer);
        }
    }
}