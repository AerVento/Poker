using System.Collections.Generic;
using System.IO;
using Game.Network;
using Game.Network.Accounts;
using Game.SO;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ShopCatalogConfigTest
    {
        [Test]
        public void ItemOverridesArePartialImmutableAndIgnoreUnknownIds()
        {
            var defaults = new ItemServerCatalog
            {
                Products = new[]
                {
                    new ServerItemDefinition
                    {
                        Id = "gift", Kind = ItemKind.Consumable, Price = 50,
                        Listed = true, MaxQuantity = 10
                    }
                }
            };
            var warnings = new List<string>();
            var result = ItemCatalogLoader.ApplyOverrides(defaults,
                "[{\"Id\":\"gift\",\"Price\":75},{\"Id\":\"missing\",\"Listed\":false}]", warnings.Add);

            Assert.AreEqual(75u, result.Products[0].Price);
            Assert.IsTrue(result.Products[0].Listed);
            Assert.AreEqual(10u, result.Products[0].MaxQuantity);
            Assert.AreEqual(50u, defaults.Products[0].Price);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("missing", warnings[0]);
        }

        [Test]
        public void CardFaceOverridesArePartialImmutableAndIgnoreUnknownIds()
        {
            var defaults = new CardFaceServerCatalog
            {
                Products = new[]
                {
                    new CardFaceOffer { Id = "default", Price = 0, Listed = true },
                    new CardFaceOffer { Id = "theme", Price = 1000, Listed = true }
                }
            };
            var warnings = new List<string>();
            var result = CardFaceCatalogLoader.ApplyOverrides(defaults,
                "[{\"Id\":\"theme\",\"Listed\":false},{\"Id\":\"missing\",\"Price\":1}]", warnings.Add);

            Assert.AreEqual(1000u, result.Products[1].Price);
            Assert.IsFalse(result.Products[1].Listed);
            Assert.IsTrue(defaults.Products[1].Listed);
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void AllPackagedShopCatalogsCreateValidServerDefaults()
        {
            Assert.DoesNotThrow(() => ItemShopCatalogSO.LoadDefault().CreateServerCatalog());
            Assert.DoesNotThrow(() => NamecardCatalogSO.LoadDefault().CreateServerCatalog());
            Assert.DoesNotThrow(() => CardFaceCatalogSO.LoadDefault().CreateServerCatalog());
        }

        [Test]
        public void DefaultShopConfigurationUsesDedicatedDirectory()
        {
            var normalized = ShopCatalogConfigManager.DefaultDirectory.Replace('\\', '/');
            StringAssert.EndsWith("/Configs/Shop", normalized);
            Assert.AreEqual(Path.Combine(ShopCatalogConfigManager.ConfigurationDirectory, ItemCatalogLoader.FileName),
                ItemCatalogLoader.ServerPath);
            Assert.AreEqual(Path.Combine(ShopCatalogConfigManager.ConfigurationDirectory, NamecardCatalogLoader.FileName),
                NamecardCatalogLoader.ServerPath);
            Assert.AreEqual(Path.Combine(ShopCatalogConfigManager.ConfigurationDirectory, CardFaceCatalogLoader.FileName),
                CardFaceCatalogLoader.ServerPath);
            Assert.Throws<System.ArgumentException>(() => ShopCatalogConfigManager.GetPath("../Items.json"));
        }
    }
}
