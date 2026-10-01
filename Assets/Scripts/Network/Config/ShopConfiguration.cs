using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Framework.Configs;
using Game.Network.Accounts;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// Configs/Shop is the only source of products: Items.json, Gifts.json, Namecards.json and CardFaces.json.
    /// The server reads them once at startup and refuses to start when they are invalid; restart to apply edits.
    /// </summary>
    public static class ShopConfiguration
    {
        public const string ItemsFile = "Items.json";
        public const string GiftsFile = "Gifts.json";
        public const string NamecardsFile = "Namecards.json";
        public const string CardFacesFile = "CardFaces.json";
        // Products whose behavior is written in code; they cannot be removed from the configuration.
        public static readonly string[] ContractIds = { "devil-contract", "archdevil-contract", "satan-contract" };
        public static readonly string[] ServiceNamecardIds = { "devil-service", "archdevil-service", "satan-service" };

        private static ShopDefinitions _server;

        /// <summary>The definitions this server process uses; loaded on first use.</summary>
        public static ShopDefinitions Server => _server ??= Load();

        public static string GetPath(string fileName) => ShopCatalogConfigManager.GetPath(fileName);

        public static ShopDefinitions Load()
        {
            try
            {
                var definitions = Parse(Read(ItemsFile), Read(GiftsFile), Read(NamecardsFile), Read(CardFacesFile));
                definitions.Validate(ContractIds, ServiceNamecardIds);
                NetworkConfigurationLoader.LogInfo("Shop configuration loaded from " + ShopCatalogConfigManager.ConfigurationDirectory +
                    ": " + definitions.Items.Length + " items, " + definitions.Gifts.Length + " gifts, " + definitions.Namecards.Length +
                    " namecards, " + definitions.CardFaces.Length + " card faces. Restart the server to apply edits.");
                return definitions;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is InvalidOperationException || exception is InvalidDataException || exception is JsonException)
            {
                throw new InvalidDataException("Invalid shop configuration in " + ShopCatalogConfigManager.ConfigurationDirectory + ":\n" +
                    exception.Message, exception);
            }
        }

        private static string Read(string fileName)
        {
            if (!ConfigManager.FileExists(ShopCatalogConfigManager.ConfigurationSection, fileName))
                throw new InvalidDataException("Missing " + GetPath(fileName));
            return ConfigManager.ReadAllText(ShopCatalogConfigManager.ConfigurationSection, fileName);
        }

        public static ShopDefinitions Parse(string items, string gifts, string namecards, string cardFaces) => new ShopDefinitions
        {
            Items = ReadArray(ItemsFile, items, entry => new ItemDefinition
            {
                Id = entry.String("Id"),
                Name = entry.String("Name"),
                Description = entry.String("Description", string.Empty),
                Icon = entry.String("Icon"),
                Kind = entry.Kind("Kind"),
                Price = entry.UInt("Price"),
                Listed = entry.Bool("Listed", true),
            }, (entry, item) => item.MaxQuantity = entry.UInt("MaxQuantity", item.Kind == ItemKind.NonConsumable ? 1u : uint.MaxValue),
                "Id", "Name", "Description", "Icon", "Kind", "Price", "Listed", "MaxQuantity"),
            Gifts = ReadArray(GiftsFile, gifts, entry => new GiftDefinition
            {
                ItemId = entry.String("ItemId"),
                Effect = entry.String("Effect"),
            }, null, "ItemId", "Effect"),
            Namecards = ReadArray(NamecardsFile, namecards, entry => new NamecardDefinition
            {
                Id = entry.String("Id"),
                Name = entry.String("Name"),
                Description = entry.String("Description", string.Empty),
                Artwork = entry.String("Artwork", string.Empty),
                Price = entry.UInt("Price", 0),
                Listed = entry.Bool("Listed", true),
                Purchasable = entry.Bool("Purchasable", true),
                Reward = entry.Bool("Reward", false),
            }, null, "Id", "Name", "Description", "Artwork", "Price", "Listed", "Purchasable", "Reward"),
            CardFaces = ReadArray(CardFacesFile, cardFaces, entry => new CardFaceDefinition
            {
                Id = entry.String("Id"),
                Name = entry.String("Name"),
                Description = entry.String("Description", string.Empty),
                Cards = entry.String("Cards", string.Empty),
                Preview = entry.String("Preview"),
                Price = entry.UInt("Price", 0),
                Listed = entry.Bool("Listed", true),
            }, null, "Id", "Name", "Description", "Cards", "Preview", "Price", "Listed"),
        };

        public static ItemServerCatalog CreateItemCatalog(ShopDefinitions definitions)
        {
            var catalog = new ItemServerCatalog
            {
                Products = definitions.Items.Select(item => new ServerItemDefinition
                {
                    Id = item.Id, Kind = item.Kind, Price = item.Price, Listed = item.Listed, MaxQuantity = item.MaxQuantity
                }).ToArray()
            };
            catalog.Validate();
            return catalog;
        }

        public static NamecardServerCatalog CreateNamecardCatalog(ShopDefinitions definitions)
        {
            // Only sellable cards are offered for purchase; the rest stay in the catalog so owners can equip them.
            var catalog = new NamecardServerCatalog
            {
                Products = definitions.Namecards.Select(card => new NamecardOffer
                {
                    Id = card.Id, Price = card.IsDefault ? 0 : card.Price, Listed = card.Sellable
                }).ToArray()
            };
            catalog.Validate();
            return catalog;
        }

        public static CardFaceServerCatalog CreateCardFaceCatalog(ShopDefinitions definitions)
        {
            var catalog = new CardFaceServerCatalog
            {
                Products = definitions.CardFaces.Select(face => new CardFaceOffer
                {
                    Id = face.Id, Price = face.Price, Listed = face.Listed
                }).ToArray()
            };
            catalog.Validate();
            return catalog;
        }

        public static string Serialize(IEnumerable<object> entries) =>
            JsonSerializer.Serialize(entries, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

        private readonly struct Entry
        {
            private readonly string _file;
            private readonly JsonElement _value;
            private readonly int _index;

            public Entry(string file, JsonElement value, int index)
            {
                _file = file;
                _value = value;
                _index = index;
            }

            private InvalidDataException Error(string field, string message) =>
                new InvalidDataException(_file + " entry " + _index + " field " + field + ": " + message);

            private bool TryGet(string field, out JsonElement value) => _value.TryGetProperty(field, out value);

            public string String(string field, string fallback = null)
            {
                if (!TryGet(field, out var value))
                    return fallback ?? throw Error(field, "is required.");
                if (value.ValueKind != JsonValueKind.String) throw Error(field, "must be a string.");
                return value.GetString();
            }

            public uint UInt(string field, uint? fallback = null)
            {
                if (!TryGet(field, out var value))
                    return fallback ?? throw Error(field, "is required.");
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out var result))
                    throw Error(field, "must be an unsigned integer.");
                return result;
            }

            public bool Bool(string field, bool fallback)
            {
                if (!TryGet(field, out var value)) return fallback;
                if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False) throw Error(field, "must be true or false.");
                return value.GetBoolean();
            }

            public ItemKind Kind(string field)
            {
                var text = String(field);
                if (!Enum.TryParse<ItemKind>(text, false, out var kind) || !Enum.IsDefined(typeof(ItemKind), kind))
                    throw Error(field, "must be Consumable or NonConsumable.");
                return kind;
            }
        }

        private static T[] ReadArray<T>(string file, string json, Func<Entry, T> create, Action<Entry, T> finish, params string[] fields)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException(file + " is empty.");
            using var document = JsonDocument.Parse(json.TrimStart('﻿'),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException(file + " must be a JSON array.");
            var allowed = new HashSet<string>(fields, StringComparer.Ordinal);
            var result = new List<T>();
            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException(file + " entry " + index + " must be an object.");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!allowed.Contains(property.Name)) throw new InvalidDataException(file + " entry " + index + ": unknown field " + property.Name + ".");
                    if (!seen.Add(property.Name)) throw new InvalidDataException(file + " entry " + index + ": duplicate field " + property.Name + ".");
                }
                var entry = new Entry(file, element, index);
                var value = create(entry);
                finish?.Invoke(entry, value);
                result.Add(value);
                index++;
            }
            return result.ToArray();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _server = null;
    }
}
