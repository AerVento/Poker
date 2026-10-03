using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Framework.Configs
{
    public static partial class ConfigManager
    {
        public static JsonContract GetContract(Type type) => _settings.ContractResolver.ResolveContract(type);

        public static object CreateDefault(Type type)
        {
            if (type == typeof(string)) return string.Empty;
            if (type.IsArray) return Array.CreateInstance(type.GetElementType(), 0);
            if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                throw new InvalidOperationException("Cannot construct configuration type " + type.FullName + ".");
            return Activator.CreateInstance(type);
        }

        public static object Load(Type type) => LoadAtPath(type, GetPath(GetAttribute(type).Path));
        public static T LoadAtPath<T>(string path) => (T)LoadAtPath(typeof(T), path);
        public static object LoadAtPath(Type type, string path)
        {
            try { return Deserialize(ReadAllTextAtPath(path), type); }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException(Path.GetFileName(path) + ": " + exception.Message, exception);
            }
        }

        public static T Deserialize<T>(string json) => (T)Deserialize(json, typeof(T));
        public static object Deserialize(string json, Type type)
        {
            try { return DeserializeCore(json, type); }
            catch (JsonException exception)
            {
                var path = exception is JsonSerializationException serialization ? serialization.Path :
                    exception is JsonReaderException reader ? reader.Path : "";
                throw new InvalidDataException("Invalid " + type.Name + " configuration at " + path + ".", exception);
            }
        }

        private static object DeserializeCore(string json, Type type)
        {
            using var reader = new JsonTextReader(new StringReader(json.TrimStart('\uFEFF')))
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal
            };
            var token = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new JsonSerializationException("Unexpected content after the configuration.");
            CheckShape(token, type, "$", null);
            var value = token.ToObject(type, JsonSerializer.Create(_settings));
            return value ?? throw new JsonSerializationException("Configuration cannot be null.");
        }

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, _settings);

        public static void Save(Type type, object value)
        {
            if (value == null || !type.IsInstanceOfType(value)) throw new ArgumentException("Configuration type mismatch.");
            WriteAllTextAtPath(GetPath(GetAttribute(type).Path), Serialize(value));
        }

        public static void CreateFile(Type type)
        {
            var path = GetPath(GetAttribute(type).Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            var json = Serialize(CreateDefault(type));
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            writer.Write(json);
        }

        private static void CheckShape(JToken token, Type type, string path, JsonConverter converter)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            var contract = GetContract(type);
            if (converter != null || contract.Converter != null || _settings.Converters.Any(c => c.CanConvert(type)) ||
                typeof(JToken).IsAssignableFrom(type) || type == typeof(object)) return;
            if (token.Type == JTokenType.Null) return; // Required/nullability are enforced by the serializer.
            if (contract is JsonObjectContract obj)
            {
                if (!(token is JObject value)) throw new JsonSerializationException(path + ": expected object.");
                var errors = new System.Collections.Generic.List<string>();
                foreach (var member in obj.Properties.Where(p => !p.Ignored && (p.Required == Required.Always || p.Required == Required.AllowNull)))
                    if (value.Property(member.PropertyName) == null) errors.Add(path + "." + member.PropertyName + ": is required.");
                foreach (var property in value.Properties())
                {
                    var member = obj.Properties.FirstOrDefault(p => p.PropertyName == property.Name && !p.Ignored);
                    if (member == null)
                    {
                        if (obj.ExtensionDataSetter == null) errors.Add(path + ": unknown field " + property.Name + ".");
                        continue;
                    }
                    try { CheckShape(property.Value, member.PropertyType, path + "." + property.Name, member.Converter); }
                    catch (InvalidDataException exception) { errors.Add(exception.Message); }
                    catch (JsonException) { errors.Add(path + "." + property.Name + ": incompatible JSON type."); }
                }
                if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
            }
            else if (contract is JsonArrayContract array)
            {
                if (!(token is JArray values)) throw new JsonSerializationException(path + ": expected array.");
                var errors = new System.Collections.Generic.List<string>();
                for (var i = 0; i < values.Count; i++)
                {
                    try { CheckShape(values[i], array.CollectionItemType, path + "[" + i + "]", array.ItemConverter); }
                    catch (InvalidDataException exception) { errors.Add(exception.Message); }
                    catch (JsonException) { errors.Add(path + "[" + i + "]: incompatible JSON type."); }
                }
                if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
            }
            else if (contract is JsonDictionaryContract dictionary)
            {
                if (!(token is JObject values)) throw new JsonSerializationException(path + ": expected dictionary.");
                foreach (var property in values.Properties()) CheckShape(property.Value, dictionary.DictionaryValueType, path + "." + property.Name, dictionary.ItemConverter);
            }
            else if (type == typeof(string) && token.Type != JTokenType.String ||
                type == typeof(bool) && token.Type != JTokenType.Boolean ||
                IsNumber(type) && token.Type != JTokenType.Integer && token.Type != JTokenType.Float ||
                IsInteger(type) && token.Type != JTokenType.Integer)
                throw new JsonSerializationException(path + ": incompatible JSON type for " + type.Name + ".");
            else if (IsNumber(type))
            {
                try { token.ToObject(type, JsonSerializer.Create(_settings)); }
                catch (Exception exception) when (exception is JsonException || exception is OverflowException)
                { throw new InvalidDataException(path + ": number is outside the range of " + type.Name + "."); }
            }
        }

        private static bool IsInteger(Type type) => type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
            type == typeof(ushort) || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong);
        private static bool IsNumber(Type type) => IsInteger(type) || type == typeof(float) || type == typeof(double) || type == typeof(decimal);
    }
}
