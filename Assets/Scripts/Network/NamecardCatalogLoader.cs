using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Game.Network.Accounts;
using UnityEngine;

namespace Game.Network
{
    public static class NamecardCatalogLoader
    {
        public const string FileName = "NamecardServerCatalog.json";

        public static NamecardServerCatalog LoadServer()
        {
            // Follow -pokerServerConfig as well as the default executable/Configs directory.
            var path = Path.Combine(Path.GetDirectoryName(NetworkConfigurationLoader.GetServerPath()), FileName);
            var catalog = LoadOrCreate(path, () =>
            {
                var asset = Resources.Load<TextAsset>("SO/Shop/NamecardServerCatalog");
                if (asset == null)
                    throw new InvalidOperationException("Missing exported namecard server catalog.");
                return asset.text;
            });
            NetworkConfigurationLoader.LogInfo($"Namecard server catalog loaded: {path}, " +
                $"{catalog.Products.Length} products, revision {catalog.Revision}. Restart the server to apply edits.");
            return catalog;
        }

        public static NamecardServerCatalog LoadOrCreate(string path, Func<string> loadDefaults)
        {
            path = Path.GetFullPath(path);
            try
            {
                if (File.Exists(path))
                    return Parse(File.ReadAllText(path));

                var json = loadDefaults();
                var catalog = Parse(json);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // Never overwrite an operator's file, even if it appeared after the existence check.
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.Write(json);
                return catalog;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
                exception is InvalidOperationException)
            {
                throw new InvalidDataException($"Unable to load namecard server catalog '{path}': {exception.Message}", exception);
            }
        }

        public static NamecardServerCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("The namecard catalog is empty.");
            try
            {
                using var document = JsonDocument.Parse(json.TrimStart('\uFEFF'));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 || root.GetArrayLength() > 500)
                    throw new InvalidDataException("The namecard catalog must be an array of 1 to 500 products.");
                var products = new List<NamecardOffer>();
                foreach (var element in root.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object)
                        throw new InvalidDataException("Each namecard product must be an object.");
                    var fields = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var field in element.EnumerateObject())
                    {
                        if ((field.Name != "Id" && field.Name != "Price" && field.Name != "Listed") || !fields.Add(field.Name))
                            throw new InvalidDataException($"Unknown or duplicate namecard field '{field.Name}'. Use Id, Price and Listed.");
                    }
                    // Missing Price/Listed must not silently turn a product into a free/listed item.
                    if (fields.Count != 3 || element.GetProperty("Id").ValueKind != JsonValueKind.String ||
                        element.GetProperty("Price").ValueKind != JsonValueKind.Number ||
                        !element.GetProperty("Price").TryGetUInt32(out var price) ||
                        (element.GetProperty("Listed").ValueKind != JsonValueKind.True &&
                         element.GetProperty("Listed").ValueKind != JsonValueKind.False))
                        throw new InvalidDataException("Each product requires a string Id, an unsigned integer Price and a boolean Listed.");
                    products.Add(new NamecardOffer
                    {
                        Id = element.GetProperty("Id").GetString(),
                        Price = price,
                        Listed = element.GetProperty("Listed").GetBoolean()
                    });
                }
                var catalog = new NamecardServerCatalog { Products = products.ToArray() };
                catalog.Validate();
                return catalog;
            }
            catch (Exception exception) when (exception is JsonException || exception is InvalidOperationException)
            {
                throw new InvalidDataException("Invalid namecard catalog: " + exception.Message, exception);
            }
        }
    }
}
