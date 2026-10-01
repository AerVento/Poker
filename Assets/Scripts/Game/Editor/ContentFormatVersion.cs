using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Game.Content;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The content format: a hash of everything a page uses to read remote content. It covers the serialized fields of
    /// every script type referenced by remote groups (recursively into serializable nested types), the script API exposed
    /// to gift Lua, and the Unity version. Pages and content builds use this same function.
    /// </summary>
    [InitializeOnLoad]
    public static class ContentFormatVersion
    {
        private static string _current;

        static ContentFormatVersion()
        {
            ClientBuild.EditorContentFormat = () => _current ??= Compute();
            AssetDatabase.importPackageCompleted += _ => _current = null;
            EditorApplication.projectChanged += () => _current = null;
        }

        public static string Compute() => Hash(Describe());

        public static string Hash(IEnumerable<string> lines)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
            return string.Concat(bytes.Take(8).Select(b => b.ToString("x2")));
        }

        public static IEnumerable<string> Describe()
        {
            var lines = new SortedSet<string>(StringComparer.Ordinal) { "unity:" + Application.unityVersion };
            var visited = new HashSet<Type>();
            foreach (var type in RemoteScriptTypes()) DescribeType(type, lines, visited);
            foreach (var line in ScriptApiLines()) lines.Add("api:" + line);
            return lines;
        }

        // Every script whose serialized data can appear in a remote bundle.
        private static IEnumerable<Type> RemoteScriptTypes()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return Array.Empty<Type>();
            var roots = settings.groups.Where(group => group != null && IsRemote(group.GetSchema<BundledAssetGroupSchema>(), settings))
                .SelectMany(group => group.entries).Select(entry => entry.AssetPath).Where(path => !string.IsNullOrEmpty(path)).ToArray();
            return AssetDatabase.GetDependencies(roots, true)
                .Where(path => path.EndsWith(".cs", StringComparison.Ordinal))
                .Select(path => AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass())
                .Where(type => type != null).Distinct().OrderBy(type => type.FullName, StringComparer.Ordinal);
        }

        private static bool IsRemote(BundledAssetGroupSchema schema, UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings) =>
            schema != null && schema.LoadPath.GetValue(settings).StartsWith(RemoteAssets.BuildOrigin + "/", StringComparison.Ordinal);

        private static void DescribeType(Type type, ISet<string> lines, ISet<Type> visited)
        {
            if (!visited.Add(type)) return;
            lines.Add("type:" + type.FullName);
            for (var current = type; current != null && !IsUnityRoot(current); current = current.BaseType)
            {
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsInitOnly || field.IsNotSerialized) continue;
                    var reference = field.IsDefined(typeof(SerializeReference), true);
                    if (!field.IsPublic && !reference && !field.IsDefined(typeof(SerializeField), true)) continue;
                    lines.Add("field:" + current.FullName + "." + field.Name + ":" + TypeName(field.FieldType) + (reference ? ":reference" : string.Empty));
                    var element = ElementType(field.FieldType);
                    if (IsNested(element)) DescribeType(element, lines, visited);
                }
            }
        }

        private static bool IsUnityRoot(Type type) => type == typeof(object) || type == typeof(UnityEngine.Object) ||
            type == typeof(ScriptableObject) || type == typeof(MonoBehaviour) || type == typeof(Behaviour) || type == typeof(Component);

        private static Type ElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : type;
        }

        private static bool IsNested(Type type) => !type.IsPrimitive && !type.IsEnum && type != typeof(string) &&
            !typeof(UnityEngine.Object).IsAssignableFrom(type) && type.IsDefined(typeof(SerializableAttribute), false) &&
            type.Assembly != typeof(object).Assembly;

        private static string TypeName(Type type)
        {
            if (!type.IsGenericType) return type.FullName ?? type.Name;
            return type.GetGenericTypeDefinition().FullName + "[" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + "]";
        }

        // Public members of types marked as content script API. Lua calls them by name, so any signature change is a format change.
        private static IEnumerable<string> ScriptApiLines()
        {
            foreach (var type in TypeCache.GetTypesWithAttribute<ContentScriptApiAttribute>().OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    switch (member)
                    {
                        case MethodInfo method when !method.IsSpecialName:
                            yield return type.FullName + "." + method.Name + "(" +
                                string.Join(",", method.GetParameters().Select(p => TypeName(p.ParameterType))) + "):" + TypeName(method.ReturnType);
                            break;
                        case PropertyInfo property:
                            yield return type.FullName + "." + property.Name + ":" + TypeName(property.PropertyType);
                            break;
                        case FieldInfo field:
                            // Constants such as the Lua bootstrap are API too: their values count.
                            yield return type.FullName + "." + field.Name + ":" + TypeName(field.FieldType) +
                                (field.IsLiteral ? "=" + Hash(new[] { Convert.ToString(field.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture) }) : string.Empty);
                            break;
                    }
                }
            }
        }
    }
}
