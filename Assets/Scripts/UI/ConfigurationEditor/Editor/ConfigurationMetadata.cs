using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Framework.Configs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEditor;

namespace Game.UI
{
    internal static class ConfigurationMetadata
    {
        private static Dictionary<string, Type> _types;
        internal static IReadOnlyDictionary<string, Type> Types
        {
            get
            {
                if (_types != null) return _types;
                var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                foreach (var type in TypeCache.GetTypesWithAttribute<ConfigAttribute>())
                {
                    var attribute = type.GetCustomAttribute<ConfigAttribute>();
                    ConfigManager.GetPath(attribute.Path); // Reject invalid paths before displaying or creating files.
                    var path = attribute.Path.Replace('\\', '/');
                    if (result.ContainsKey(path)) throw new InvalidOperationException("重复配置路径：" + path);
                    result.Add(path, type);
                }
                return _types = result;
            }
        }

        internal static Type Find(string path) => Types.TryGetValue(path.Replace('\\', '/'), out var type) ? type : null;
        internal static IEnumerable<JsonProperty> Members(Type type) => type != null && ConfigManager.GetContract(type) is JsonObjectContract contract
            ? contract.Properties.Where(p => !p.Ignored && p.Readable && p.Writable)
            : Enumerable.Empty<JsonProperty>();
        internal static Type ElementType(Type type) => type != null && ConfigManager.GetContract(type) is JsonArrayContract contract
            ? contract.CollectionItemType : null;
        internal static T Attribute<T>(JsonProperty member) where T : Attribute =>
            member?.AttributeProvider?.GetAttributes(typeof(T), true).OfType<T>().FirstOrDefault();

        internal static JToken NewValue(Type type)
        {
            if (type == null || typeof(JToken).IsAssignableFrom(type)) throw new InvalidOperationException("此元素需要选择具体的数据类型。");
            return ConfigurationDocument.Parse(ConfigManager.Serialize(ConfigManager.CreateDefault(Nullable.GetUnderlyingType(type) ?? type)));
        }

        internal static bool IsRequired(JsonProperty member) =>
            member.Required == Required.Always || member.Required == Required.AllowNull;

        internal static JToken MemberDefault(Type type, JsonProperty member)
        {
            var instance = ConfigManager.CreateDefault(type);
            var value = member.ValueProvider.GetValue(instance) ??
                ConfigManager.CreateDefault(Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType);
            // Serialize this value with the field's converter, even when null/default omission is configured.
            if (member.Converter != null)
            {
                using var writer = new JTokenWriter();
                member.Converter.WriteJson(writer, value, JsonSerializer.CreateDefault());
                return ConfigurationDocument.Parse(writer.Token.ToString(Formatting.None));
            }
            return ConfigurationDocument.Parse(ConfigManager.Serialize(value));
        }

        internal static bool FillRequired(JToken token, Type type, int depth = 0)
        {
            if (depth > 20) return false;
            var changed = false;
            if (token is JObject obj)
            {
                foreach (var member in Members(type))
                {
                    var property = obj.Property(member.PropertyName);
                    if (IsRequired(member) && (property == null || property.Value.Type == JTokenType.Null))
                    {
                        obj[member.PropertyName] = MemberDefault(type, member);
                        property = obj.Property(member.PropertyName);
                        changed = true;
                    }
                    if (property != null) changed |= FillRequired(property.Value, member.PropertyType, depth + 1);
                }
            }
            else if (token is JArray array)
                for (var index = 0; index < array.Count; index++)
                    changed |= FillRequired(array[index], TaskParameterType(array, index) ?? ElementType(type), depth + 1);
            return changed;
        }

        // params of tasks, conditions and objectives are positional; their types depend on the owner's type.
        internal static Type TaskParameterType(JArray array, int index)
        {
            if (!(array.Parent is JProperty property) || property.Name != "params" || !(property.Parent is JObject owner)) return null;
            var types = (owner["type"]?.Value<string>()) switch
            {
                "DirectClaimWhenLesserThan" or "DirectClaimWhenAtMost" => new[] { typeof(uint) },
                "PlayHands" or "WinHands" => new[] { typeof(int) },
                "FormPattern" or "WinWithPattern" => new[] { typeof(Game.Facts.FormedPattern), typeof(int) },
                "CountHands" or "CountDistinctHands" => new[] { typeof(int), typeof(TaskConditionConfigData[]) },
                "AllObjectives" => new[] { typeof(TaskObjectiveConfigData[]) },
                "PatternObserved" => new[] { typeof(Game.Facts.FormedPattern) },
                _ => Array.Empty<Type>()
            };
            return index < types.Length ? types[index] : null;
        }
    }
}
