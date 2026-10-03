using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Game.Network;
using Framework.Configs;
using System.Reflection;

namespace Game.UI
{
    internal static class ConfigurationDocument
    {
        internal enum Audience { Unknown, Client, Server, Both }

        internal static Audience Classify(string relativePath)
        {
            var type = ConfigurationMetadata.Find(relativePath);
            switch (type?.GetCustomAttribute<ConfigAttribute>()?.Usage ?? ConfigUsage.Unspecified)
            {
                case ConfigUsage.Client: return Audience.Client;
                case ConfigUsage.Server: return Audience.Server;
                case ConfigUsage.Both: return Audience.Both;
                default: return Audience.Unknown;
            }
        }

        internal static string AudienceLabel(Audience audience)
        {
            switch (audience)
            {
                case Audience.Client: return "仅客户端";
                case Audience.Server: return "仅服务器";
                case Audience.Both: return "客户端与服务器";
                default: return "未分类";
            }
        }

        internal static string[] Discover(string directory)
        {
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                .Select(path => path.Substring(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1)
                    .Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static JToken Parse(string json)
        {
            using var document = JsonDocument.Parse(json.TrimStart('\uFEFF'));
            return Read(document.RootElement);
        }

        internal static List<string> ValidateAll(string directory)
        {
            var errors = new List<string>();
            foreach (var file in Discover(directory))
                Check(errors, file, () => Parse(File.ReadAllText(Path.Combine(directory, file))));
            foreach (var entry in ConfigurationMetadata.Types)
                Check(errors, entry.Key, () => ConfigManager.LoadAtPath(entry.Value, Path.Combine(directory, entry.Key)));
            ValidateNetwork(directory, errors);
            Check(errors, "服务端业务配置", () => ServerConfiguration.TryLoad(errors, logSuccess: false));
            return errors;
        }

        internal static void ValidateNetwork(string directory, List<string> errors)
        {
            Check(errors, "Network/client-network.json", () =>
                NetworkConfiguration.ParseClient(File.ReadAllText(Path.Combine(directory, "Network/client-network.json"))));
            Check(errors, "Network/server-network.json", () =>
                NetworkConfiguration.ParseServer(File.ReadAllText(Path.Combine(directory, "Network/server-network.json"))).ValidateCertificate());
        }

        private static void Check(List<string> errors, string file, Action check)
        {
            try { check(); }
            catch (Exception exception) { errors.Add(file + ": " + exception.Message); }
        }

        private static JToken Read(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var result = new JObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        if (result.Property(property.Name) != null)
                            throw new InvalidDataException("重复字段：" + property.Name);
                        result.Add(property.Name, Read(property.Value));
                    }
                    return result;
                case JsonValueKind.Array:
                    return new JArray(element.EnumerateArray().Select(Read));
                // Retain the original numeric literal, including integers beyond Int64 and decimal precision.
                case JsonValueKind.Number: return new JRaw(element.GetRawText());
                case JsonValueKind.String: return new JValue(element.GetString());
                case JsonValueKind.True: return new JValue(true);
                case JsonValueKind.False: return new JValue(false);
                default: return JValue.CreateNull();
            }
        }

        internal static string Serialize(JToken root, bool indented = false)
        {
            return root.ToString(indented ? Formatting.Indented : Formatting.None);
        }

        internal static string Decode(byte[] bytes)
        {
            return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        }

        internal static void Save(string path, string draft, byte[] expected)
        {
            var parsed = Parse(draft);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(expected))
                throw new IOException("文件已在外部修改或删除。请先重新读取，或复制当前 JSON 保留修改。");
            var original = Decode(expected);
            var newline = original.Contains("\r\n") ? "\r\n" : "\n";
            var output = Serialize(parsed, true).Replace("\r\n", "\n").Replace("\n", newline);
            if (original.EndsWith("\n", StringComparison.Ordinal)) output += newline;
            var bom = expected.Length >= 3 && expected[0] == 0xEF && expected[1] == 0xBB && expected[2] == 0xBF;
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, output, new UTF8Encoding(bom));
                // Check again after preparing the replacement; never truncate the original on a failed write.
                if (!File.ReadAllBytes(path).SequenceEqual(expected))
                    throw new IOException("文件已在外部修改，请先重新读取。");
                File.Replace(temporary, path, null);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
