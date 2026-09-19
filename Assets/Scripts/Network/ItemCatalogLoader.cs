using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Network.Accounts;
using Game.SO;

namespace Game.Network
{
    public static class ItemCatalogLoader
    {
        public const string FileName = "Items.json";
        public static string ServerPath => ShopCatalogConfigManager.GetPath(FileName);

        public static ItemServerCatalog LoadServer() => ShopCatalogConfigManager.Load(
            "item shop", FileName, () => ItemShopCatalogSO.LoadDefault().CreateServerCatalog(), ApplyOverrides);

        public static string ExportDefaults(ItemShopCatalogSO catalog)
            => JsonSerializer.Serialize(catalog.CreateServerCatalog().Products, new JsonSerializerOptions { WriteIndented = true });

        // Merge into a copy. Neither the shared SO nor the caller's defaults are mutated.
        public static ItemServerCatalog ApplyOverrides(ItemServerCatalog defaults, string json, Action<string> warning = null)
        {
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));
            defaults.Validate();
            var products = defaults.Products.Select(p => new ServerItemDefinition
            {
                Id = p.Id, Kind = p.Kind, Price = p.Price, Listed = p.Listed, MaxQuantity = p.MaxQuantity
            }).ToArray();
            var byId = products.ToDictionary(p => p.Id, StringComparer.Ordinal);
            ShopCatalogConfigManager.ApplyOverrideArray("item shop", json, byId, (product, field, value) =>
            {
                switch (field)
                {
                    case "Price": product.Price = ShopCatalogConfigManager.ReadUInt(value, "Price"); break;
                    case "MaxQuantity": product.MaxQuantity = ShopCatalogConfigManager.ReadUInt(value, "MaxQuantity"); break;
                    case "Listed": product.Listed = ShopCatalogConfigManager.ReadBoolean(value, "Listed"); break;
                    case "Kind":
                        if (ShopCatalogConfigManager.ReadUInt(value, "Kind") != (uint)product.Kind)
                            throw new InvalidDataException("Item Kind must match the shop SO: " + product.Id);
                        break;
                    default: throw new InvalidDataException("Unknown item shop override field: " + field);
                }
            }, warning);
            var result = new ItemServerCatalog { Products = products };
            result.Validate();
            return result;
        }
    }
}
