using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Framework.Configs;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace Game
{
    // Keep reflection-only constructors and members available in IL2CPP players.
    public sealed class ConfigurationLinker : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var types = new HashSet<Type>();
            void Visit(Type type)
            {
                if (type == null || type.IsPrimitive || type == typeof(string) || typeof(JToken).IsAssignableFrom(type) || !types.Add(type)) return;
                if (ConfigManager.GetContract(type) is JsonArrayContract array) Visit(array.CollectionItemType);
                if (ConfigManager.GetContract(type) is JsonDictionaryContract dictionary) Visit(dictionary.DictionaryValueType);
                if (ConfigManager.GetContract(type) is JsonObjectContract obj)
                    foreach (var property in obj.Properties.Where(p => !p.Ignored)) Visit(property.PropertyType);
            }
            foreach (var type in TypeCache.GetTypesWithAttribute<ConfigAttribute>()) Visit(type);
            // Positional task parameters are JSON tokens; their actual schemas are selected by task type.
            Visit(typeof(TaskObjectiveConfigData));
            Visit(typeof(TaskConditionConfigData));
            var path = Path.GetFullPath("Library/Configuration/link.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            new XDocument(new XElement("linker", types.Where(t => !t.IsArray && !t.IsGenericType).GroupBy(t => t.Assembly.GetName().Name)
                .Select(group => new XElement("assembly", new XAttribute("fullname", group.Key),
                    group.Select(type => new XElement("type", new XAttribute("fullname", type.FullName.Replace('+', '/')),
                        new XAttribute("preserve", "all"))))))).Save(path);
            return path;
        }
    }
}
