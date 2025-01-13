using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace Framework.Serialization
{
    public class RectConverter : JsonConverter<Rect>
    {
        public override Rect ReadJson(JsonReader reader, Type objectType, Rect existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            float x = obj.GetValue("x").Value<float>();
            float y = obj.GetValue("y").Value<float>();
            float width = obj.GetValue("width").Value<float>();
            float height = obj.GetValue("height").Value<float>();
            return new Rect(x, y, width, height);
        }

        public override void WriteJson(JsonWriter writer, Rect value, JsonSerializer serializer)
        {
            JObject obj = new JObject() { { "x", value.x }, { "y", value.y }, { "width", value.width }, { "height", value.height } };
            obj.WriteTo(writer);
        }
    }
}