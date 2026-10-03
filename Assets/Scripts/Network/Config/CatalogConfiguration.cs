using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Framework.Configs;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// The item and shop catalogs: Configs/Items (Items, Namecards, CardFaces, Gifts, Contracts) and Configs/Shop/Shop.json.
    /// The server reads them once at startup and refuses to start when any file is invalid; restart to apply edits.
    /// </summary>
    public static class CatalogConfiguration
    {
        public const string ItemsSection = "Items";
        public const string ShopSection = "Shop";
        public const string ItemsFile = "Items.json";
        public const string NamecardsFile = "Namecards.json";
        public const string CardFacesFile = "CardFaces.json";
        public const string GiftsFile = "Gifts.json";
        public const string ContractsFile = "Contracts.json";
        public const string ShopFile = "Shop.json";

        /// <summary>Every catalog file as (section, file name).</summary>
        public static readonly (string Section, string File)[] Files =
        {
            (ItemsSection, ItemsFile), (ItemsSection, NamecardsFile), (ItemsSection, CardFacesFile),
            (ItemsSection, GiftsFile), (ItemsSection, ContractsFile), (ShopSection, ShopFile),
        };

        private static GameCatalog _server;

        /// <summary>The catalog this server process uses; loaded on first use.</summary>
        public static GameCatalog Server => _server ??= Load();

        public static string GetPath(string section, string fileName) => ConfigManager.GetPath(section, fileName);

        public static GameCatalog Load()
        {
            var errors = new List<string>();
            var catalog = TryLoad(errors);
            if (errors.Count > 0)
                throw new InvalidDataException("Invalid item or shop configuration in " + ConfigManager.RootDirectory + ":\n" + string.Join("\n", errors));
            NetworkConfigurationLoader.LogInfo("Item catalog loaded: " + catalog.Items.Length + " items, " + catalog.Namecards.Length +
                " namecards, " + catalog.CardFaces.Length + " card faces, " + catalog.Gifts.Length + " gifts, " + catalog.Contracts.Length +
                " contracts, " + catalog.Shop.Length + " shop offers. Restart the server to apply edits.");
            return catalog;
        }

        /// <summary>Reads and validates every file, adding each problem to errors instead of stopping at the first.</summary>
        public static GameCatalog TryLoad(List<string> errors)
        {
            var before = errors.Count;
            T Read<T>() where T : class
            {
                try { return ConfigManager.Load<T>(); }
                catch (Exception exception) { errors.Add(typeof(T).Name + ": " + exception.Message); return null; }
            }
            var items = Read<ItemsConfigData>();
            var namecards = Read<NamecardsConfigData>();
            var faces = Read<CardFacesConfigData>();
            var gifts = Read<GiftsConfigData>();
            var contracts = Read<ContractsConfigData>();
            var shop = Read<ShopConfigData>();
            if (errors.Count > before) return null;
            var catalog = Build(items, namecards, faces, gifts, contracts, shop);
            errors.AddRange(catalog.GetErrors());
            return errors.Count > before ? null : catalog;
        }

        private static GameCatalog Build(ItemsConfigData items, NamecardsConfigData namecards, CardFacesConfigData faces,
            GiftsConfigData gifts, ContractsConfigData contracts, ShopConfigData shop)
        {
            return new GameCatalog
            {
                Items = ConfigurationValidation.Map<ItemDefinition[]>(items),
                Namecards = ConfigurationValidation.Map<NamecardDefinition[]>(namecards),
                CardFaces = ConfigurationValidation.Map<CardFaceDefinition[]>(faces),
                Gifts = ConfigurationValidation.Map<GiftDefinition[]>(gifts),
                Contracts = ConfigurationValidation.Map<ContractDefinition[]>(contracts.Contracts),
                ServiceNamecards = ConfigurationValidation.Map<ServiceNamecardDefinition[]>(contracts.ServiceNamecards),
                Shop = ConfigurationValidation.Map<ShopOffer[]>(shop)
            };
        }

        /// <summary>Parses the six files; syntax and field errors go to errors and the affected file is left empty.</summary>
        public static GameCatalog Parse(string items, string namecards, string cardFaces, string gifts, string contracts,
            string shop, List<string> errors)
        {
            T Read<T>(string text, string file) where T : new()
            {
                try { return ConfigManager.Deserialize<T>(text); }
                catch (Exception exception)
                {
                    if (exception.InnerException is Newtonsoft.Json.JsonReaderException)
                        errors.Add(file + ": invalid JSON.");
                    else
                        errors.AddRange(exception.Message.Split('\n').Select(message => file + " " +
                            System.Text.RegularExpressions.Regex.Replace(message, @"\$\[(\d+)\]\.([A-Za-z0-9_]+)", "entry $1 field $2")));
                    return new T();
                }
            }
            return Build(Read<ItemsConfigData>(items, ItemsFile), Read<NamecardsConfigData>(namecards, NamecardsFile),
                Read<CardFacesConfigData>(cardFaces, CardFacesFile), Read<GiftsConfigData>(gifts, GiftsFile),
                Read<ContractsConfigData>(contracts, ContractsFile), Read<ShopConfigData>(shop, ShopFile));
        }

        /// <summary>Writes entries with the indentation and escaping used by the checked-in files.</summary>
        public static string Serialize(object value) =>
            JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

        /// <summary>One configuration object. Reading a field records a precise error and returns a harmless default.</summary>
        public readonly struct Entry
        {
            private readonly string _label;
            private readonly JsonElement _value;
            private readonly List<string> _errors;

            public Entry(string label, JsonElement value, List<string> errors)
            {
                _label = label;
                _value = value;
                _errors = errors;
            }

            private void Error(string field, string message) => _errors.Add(_label + " field " + field + ": " + message);

            public string String(string field, string fallback = null)
            {
                if (!_value.TryGetProperty(field, out var value))
                {
                    if (fallback == null) Error(field, "is required.");
                    return fallback;
                }
                if (value.ValueKind == JsonValueKind.String) return value.GetString();
                Error(field, "must be a string.");
                return fallback;
            }

            public uint UInt(string field, uint? fallback = null)
            {
                if (!_value.TryGetProperty(field, out var value))
                {
                    if (fallback == null) Error(field, "is required.");
                    return fallback ?? 0;
                }
                if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var result)) return result;
                Error(field, "must be an unsigned integer up to " + uint.MaxValue + ".");
                return fallback ?? 0;
            }

            public bool Bool(string field)
            {
                if (!_value.TryGetProperty(field, out var value))
                {
                    Error(field, "is required.");
                    return false;
                }
                if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) return value.GetBoolean();
                Error(field, "must be true or false.");
                return false;
            }
        }

        public static IEnumerable<Entry> ReadArray(string file, string json, List<string> errors, params string[] fields)
        {
            using var document = ParseDocument(file, json, errors);
            if (document == null) return Array.Empty<Entry>();
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add(file + ": must be a JSON array.");
                return Array.Empty<Entry>();
            }
            return ReadEntries(file, file, document.RootElement.Clone(), errors, fields);
        }

        private static IEnumerable<Entry> ReadArray(string file, JsonElement? root, string property, List<string> errors, params string[] fields)
        {
            if (root == null) return Array.Empty<Entry>();
            if (!root.Value.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                errors.Add(file + ": " + property + " must be a JSON array.");
                return Array.Empty<Entry>();
            }
            return ReadEntries(file, file + " " + property, array, errors, fields);
        }

        public static JsonElement? ReadObject(string file, string json, List<string> errors, params string[] fields)
        {
            using var document = ParseDocument(file, json, errors);
            if (document == null) return null;
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add(file + ": must be a JSON object.");
                return null;
            }
            var before = errors.Count;
            CheckFields(file, document.RootElement, errors, fields);
            return errors.Count > before ? (JsonElement?)null : document.RootElement.Clone();
        }

        private static List<Entry> ReadEntries(string file, string label, JsonElement array, List<string> errors, string[] fields)
        {
            var result = new List<Entry>();
            var index = 0;
            foreach (var element in array.EnumerateArray())
            {
                var entryLabel = label + " entry " + index++;
                if (element.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(entryLabel + ": must be an object.");
                    continue;
                }
                CheckFields(entryLabel, element, errors, fields);
                result.Add(new Entry(entryLabel, element, errors));
            }
            return result;
        }

        private static void CheckFields(string label, JsonElement element, List<string> errors, string[] fields)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!fields.Contains(property.Name)) errors.Add(label + ": unknown field " + property.Name + ".");
                else if (!seen.Add(property.Name)) errors.Add(label + ": duplicate field " + property.Name + ".");
            }
        }

        private static JsonDocument ParseDocument(string file, string json, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add(file + ": is empty.");
                return null;
            }
            try
            {
                return JsonDocument.Parse(json.TrimStart('﻿'),
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (JsonException exception)
            {
                errors.Add(file + ": invalid JSON (" + exception.Message + ").");
                return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _server = null;
    }
}
