using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Framework.Serialization
{
    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            float x = obj.GetValue("x").Value<float>();
            float y = obj.GetValue("y").Value<float>();
            float z = obj.GetValue("z").Value<float>();
            return new Vector3(x, y, z);
        }

        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            JObject obj = new JObject() { { "x", value.x }, { "y", value.y }, { "z", value.z } };
            obj.WriteTo(writer);
        }
    }

    public class Vector2Converter : JsonConverter<Vector2>
    {
        public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            float x = obj.GetValue("x").Value<float>();
            float y = obj.GetValue("y").Value<float>();
            return new Vector2(x, y);
        }

        public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
        {
            JObject obj = new JObject() { { "x", value.x }, { "y", value.y } };
            obj.WriteTo(writer);
        }
    }

    public class Vector3IntConverter : JsonConverter<Vector3Int>
    {
        public override Vector3Int ReadJson(JsonReader reader, Type objectType, Vector3Int existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            int x = obj.GetValue("x").Value<int>();
            int y = obj.GetValue("y").Value<int>();
            int z = obj.GetValue("z").Value<int>();
            return new Vector3Int(x, y, z);
        }

        public override void WriteJson(JsonWriter writer, Vector3Int value, JsonSerializer serializer)
        {
            JObject obj = new JObject() { { "x", value.x }, { "y", value.y }, { "z", value.z } };
            obj.WriteTo(writer);
        }
    }

    public class Vector2IntConverter : JsonConverter<Vector2Int>
    {
        public override Vector2Int ReadJson(JsonReader reader, Type objectType, Vector2Int existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            int x = obj.GetValue("x").Value<int>();
            int y = obj.GetValue("y").Value<int>();
            return new Vector2Int(x, y);
        }

        public override void WriteJson(JsonWriter writer, Vector2Int value, JsonSerializer serializer)
        {
            JObject obj = new JObject() { { "x", value.x }, { "y", value.y } };
            obj.WriteTo(writer);
        }
    }
}