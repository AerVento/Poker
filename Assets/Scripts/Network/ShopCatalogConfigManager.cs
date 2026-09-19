using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Framework.Configs;
using UnityEngine;

namespace Game.Network
{
    public static class ShopCatalogConfigManager
    {
        public const string ConfigurationSection = "Shop";
        public static string DefaultDirectory => ConfigManager.GetDirectory(ConfigurationSection);
        public static string ConfigurationDirectory => DefaultDirectory;

        public static string GetPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
                throw new ArgumentException("Shop configuration file name must not contain a directory.", nameof(fileName));
            return ConfigManager.GetPath(ConfigurationSection, fileName);
        }

        public static T Load<T>(string catalogName, string fileName, Func<T> loadDefaults,
            Func<T, string, Action<string>, T> applyOverrides)
        {
            if (string.IsNullOrWhiteSpace(catalogName)) throw new ArgumentException(nameof(catalogName));
            if (loadDefaults == null) throw new ArgumentNullException(nameof(loadDefaults));
            if (applyOverrides == null) throw new ArgumentNullException(nameof(applyOverrides));

            var path = GetPath(fileName);
            try
            {
                var defaults = loadDefaults();
                var hasOverrides = ConfigManager.FileExists(ConfigurationSection, fileName);
                var catalog = hasOverrides
                    ? applyOverrides(defaults, ConfigManager.ReadAllText(ConfigurationSection, fileName),
                        warning => LogWarning(catalogName, path, warning))
                    : defaults;
                NetworkConfigurationLoader.LogInfo($"{catalogName} catalog loaded; overrides: {path} ({hasOverrides}). " +
                    "Restart the server to apply edits.");
                return catalog;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is InvalidDataException || exception is InvalidOperationException || exception is ArgumentException)
            {
                throw new InvalidDataException($"Unable to load {catalogName} catalog overrides '{path}': {exception.Message}", exception);
            }
        }

        internal static void ApplyOverrideArray<TProduct>(string catalogName, string json,
            IDictionary<string, TProduct> productsById, Action<TProduct, string, JsonElement> applyField,
            Action<string> warning = null)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException(catalogName + " overrides cannot be empty; use [] for no overrides.");
            if (productsById == null) throw new ArgumentNullException(nameof(productsById));
            if (applyField == null) throw new ArgumentNullException(nameof(applyField));

            try
            {
                using var document = JsonDocument.Parse(json.TrimStart('\uFEFF'));
                if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > 500)
                    throw new InvalidDataException(catalogName + " overrides must be an array of at most 500 entries.");

                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object)
                        throw new InvalidDataException("Each " + catalogName + " override must be an object.");

                    string id = null;
                    var fields = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var field in entry.EnumerateObject())
                    {
                        if (!fields.Add(field.Name))
                            throw new InvalidDataException("Duplicate " + catalogName + " override field: " + field.Name);
                        if (field.Name == "Id")
                        {
                            if (field.Value.ValueKind != JsonValueKind.String)
                                throw new InvalidDataException("Each " + catalogName + " override requires a string Id.");
                            id = field.Value.GetString();
                        }
                    }

                    if (id == null)
                        throw new InvalidDataException("Each " + catalogName + " override requires a string Id.");
                    if (!ids.Add(id))
                        throw new InvalidDataException("Duplicate " + catalogName + " override ID: " + id);
                    if (!productsById.TryGetValue(id, out var product))
                    {
                        warning?.Invoke("Ignoring unknown override ID: " + id);
                        continue;
                    }

                    foreach (var field in entry.EnumerateObject())
                        if (field.Name != "Id")
                            applyField(product, field.Name, field.Value);
                }
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Invalid " + catalogName + " overrides: " + exception.Message, exception);
            }
        }

        internal static uint ReadUInt(JsonElement value, string field)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out var result))
                throw new InvalidDataException(field + " must be an unsigned integer.");
            return result;
        }

        internal static bool ReadBoolean(JsonElement value, string field)
        {
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                throw new InvalidDataException(field + " must be a boolean.");
            return value.GetBoolean();
        }

        private static void LogWarning(string catalogName, string path, string warning)
        {
            var message = $"{catalogName} catalog overrides '{path}': {warning}";
            Framework.Log.Log.WriteWarn(message);
#if !UNITY_EDITOR
            Debug.LogWarning(message);
#endif
        }
    }
}
