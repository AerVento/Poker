using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Network.Accounts;
using Game.SO;

namespace Game.Network
{
    public static class NamecardCatalogLoader
    {
        public const string FileName = "Namecards.json";
        public static string ServerPath => ShopCatalogConfigManager.GetPath(FileName);

        public static NamecardServerCatalog LoadServer() => ShopCatalogConfigManager.Load(
            "namecard shop", FileName, () => NamecardCatalogSO.LoadDefault().CreateServerCatalog(), ApplyOverrides);

        public static string ExportDefaults(NamecardCatalogSO catalog)
            => JsonSerializer.Serialize(catalog.CreateServerCatalog().Products, new JsonSerializerOptions { WriteIndented = true });

        public static NamecardServerCatalog ApplyOverrides(NamecardServerCatalog defaults, string json,
            Action<string> warning = null)
        {
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));
            defaults.Validate();
            var products = defaults.Products.Select(p => new NamecardOffer
            {
                Id = p.Id, Price = p.Price, Listed = p.Listed
            }).ToArray();
            var byId = products.ToDictionary(p => p.Id, StringComparer.Ordinal);
            ShopCatalogConfigManager.ApplyOverrideArray("namecard shop", json, byId, (product, field, value) =>
            {
                switch (field)
                {
                    case "Price": product.Price = ShopCatalogConfigManager.ReadUInt(value, "Price"); break;
                    case "Listed": product.Listed = ShopCatalogConfigManager.ReadBoolean(value, "Listed"); break;
                    default: throw new InvalidDataException("Unknown namecard shop override field: " + field);
                }
            }, warning);
            var result = new NamecardServerCatalog { Products = products };
            result.Validate();
            return result;
        }
    }
}
