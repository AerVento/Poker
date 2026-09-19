using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Network.Accounts;
using Game.SO;

namespace Game.Network
{
    public static class CardFaceCatalogLoader
    {
        public const string FileName = "CardFaces.json";
        public static string ServerPath => ShopCatalogConfigManager.GetPath(FileName);

        public static CardFaceServerCatalog LoadServer() => ShopCatalogConfigManager.Load(
            "card face shop", FileName, () => CardFaceCatalogSO.LoadDefault().CreateServerCatalog(), ApplyOverrides);

        public static string ExportDefaults(CardFaceCatalogSO catalog)
            => JsonSerializer.Serialize(catalog.CreateServerCatalog().Products, new JsonSerializerOptions { WriteIndented = true });

        public static CardFaceServerCatalog ApplyOverrides(CardFaceServerCatalog defaults, string json,
            Action<string> warning = null)
        {
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));
            defaults.Validate();
            var products = defaults.Products.Select(p => new CardFaceOffer
            {
                Id = p.Id, Price = p.Price, Listed = p.Listed
            }).ToArray();
            var byId = products.ToDictionary(p => p.Id, StringComparer.Ordinal);
            ShopCatalogConfigManager.ApplyOverrideArray("card face shop", json, byId, (product, field, value) =>
            {
                switch (field)
                {
                    case "Price": product.Price = ShopCatalogConfigManager.ReadUInt(value, "Price"); break;
                    case "Listed": product.Listed = ShopCatalogConfigManager.ReadBoolean(value, "Listed"); break;
                    default: throw new InvalidDataException("Unknown card face shop override field: " + field);
                }
            }, warning);
            var result = new CardFaceServerCatalog { Products = products };
            result.Validate();
            return result;
        }
    }
}
