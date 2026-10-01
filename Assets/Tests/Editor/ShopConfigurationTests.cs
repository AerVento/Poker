using System;
using System.IO;
using System.Linq;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ShopConfigurationTests
    {
        private const string Items = @"[
  { ""Id"": ""devil-contract"", ""Name"": ""恶魔契约"", ""Icon"": ""devil-contract"", ""Kind"": ""Consumable"", ""Price"": 0 },
  { ""Id"": ""rotten-egg"", ""Name"": ""臭鸡蛋"", ""Icon"": ""rotten-egg"", ""Kind"": ""Consumable"", ""Price"": 50 },
  { ""Id"": ""trophy"", ""Name"": ""奖杯"", ""Description"": ""永久收藏"", ""Icon"": ""trophy"", ""Kind"": ""NonConsumable"", ""Price"": 900, ""Listed"": false }
]";
        private const string Gifts = @"[ { ""ItemId"": ""rotten-egg"", ""Effect"": ""gifts/rotten-egg"" } ]";
        private const string Namecards = @"[
  { ""Id"": ""default"", ""Name"": ""经典"" },
  { ""Id"": ""galaxy"", ""Name"": ""星河"", ""Artwork"": ""namecards/galaxy"", ""Price"": 50000 },
  { ""Id"": ""devil-service"", ""Name"": ""狱契之印"", ""Artwork"": ""namecards/devil-service"", ""Purchasable"": false },
  { ""Id"": ""straight-flush"", ""Name"": ""同花顺"", ""Artwork"": ""namecards/straight-flush"", ""Reward"": true }
]";
        private const string CardFaces = @"[
  { ""Id"": ""default"", ""Name"": ""经典牌面"", ""Preview"": ""default"" },
  { ""Id"": ""cat-court"", ""Name"": ""猫猫宫廷"", ""Cards"": ""cardfaces/cat-court"", ""Preview"": ""cat-court"", ""Price"": 50000 }
]";

        [Test]
        public void ParsesCompleteDefinitionsWithDefaults()
        {
            var definitions = ShopConfiguration.Parse(Items, Gifts, Namecards, CardFaces);
            definitions.Validate(new[] { "devil-contract" }, new[] { "devil-service" });
            var trophy = definitions.FindItem("trophy");
            Assert.AreEqual(ItemKind.NonConsumable, trophy.Kind);
            Assert.AreEqual(1u, trophy.MaxQuantity);
            Assert.IsFalse(trophy.Listed);
            Assert.AreEqual(uint.MaxValue, definitions.FindItem("rotten-egg").MaxQuantity);
            Assert.AreEqual(string.Empty, definitions.FindItem("rotten-egg").Description);
            Assert.AreEqual("gifts/rotten-egg", definitions.FindGift("rotten-egg").Effect);
            Assert.AreEqual("default", definitions.FindNamecard(null).Id);
            Assert.IsFalse(definitions.FindNamecard("devil-service").CanPurchase);
            Assert.IsFalse(definitions.FindNamecard("straight-flush").Sellable);
            Assert.IsTrue(definitions.FindNamecard("galaxy").Sellable);
        }

        [Test]
        public void ServerCatalogsOfferOnlySellableNamecards()
        {
            var definitions = ShopConfiguration.Parse(Items, Gifts, Namecards, CardFaces);
            var namecards = ShopConfiguration.CreateNamecardCatalog(definitions);
            Assert.IsTrue(namecards.Products.Single(p => p.Id == "galaxy").Listed);
            Assert.IsFalse(namecards.Products.Single(p => p.Id == "devil-service").Listed);
            Assert.IsFalse(namecards.Products.Single(p => p.Id == "straight-flush").Listed);
            Assert.AreEqual(3, ShopConfiguration.CreateItemCatalog(definitions).Products.Length);
            Assert.AreEqual(50000u, ShopConfiguration.CreateCardFaceCatalog(definitions).Products.Single(p => p.Id == "cat-court").Price);
        }

        [Test]
        public void RejectsUnknownFieldsWrongTypesAndMissingRequiredFields()
        {
            Assert.Throws<InvalidDataException>(() => ShopConfiguration.Parse(
                @"[{ ""Id"": ""a"", ""Name"": ""A"", ""Icon"": ""a"", ""Kind"": ""Consumable"", ""Price"": 1, ""Prise"": 2 }]", Gifts, Namecards, CardFaces));
            Assert.Throws<InvalidDataException>(() => ShopConfiguration.Parse(
                @"[{ ""Id"": ""a"", ""Name"": ""A"", ""Icon"": ""a"", ""Kind"": ""Consumable"", ""Price"": -1 }]", Gifts, Namecards, CardFaces));
            Assert.Throws<InvalidDataException>(() => ShopConfiguration.Parse(
                @"[{ ""Id"": ""a"", ""Icon"": ""a"", ""Kind"": ""Consumable"", ""Price"": 1 }]", Gifts, Namecards, CardFaces));
            Assert.Throws<InvalidDataException>(() => ShopConfiguration.Parse(
                @"[{ ""Id"": ""a"", ""Name"": ""A"", ""Icon"": ""a"", ""Kind"": ""Magic"", ""Price"": 1 }]", Gifts, Namecards, CardFaces));
            Assert.Throws<InvalidDataException>(() => ShopConfiguration.Parse("{}", Gifts, Namecards, CardFaces));
        }

        [Test]
        public void ValidationReportsBrokenReferencesAndRequiredProducts()
        {
            var badGift = ShopConfiguration.Parse(Items, @"[ { ""ItemId"": ""trophy"", ""Effect"": ""gifts/trophy"" }, { ""ItemId"": ""missing"", ""Effect"": ""x"" } ]",
                Namecards, CardFaces);
            var error = Assert.Throws<InvalidOperationException>(() => badGift.Validate());
            StringAssert.Contains("Gift trophy: must be a consumable item.", error.Message);
            StringAssert.Contains("Gift missing: must be an item in Items.json.", error.Message);

            var noDefault = ShopConfiguration.Parse(Items, Gifts, @"[ { ""Id"": ""galaxy"", ""Name"": ""星河"", ""Artwork"": ""namecards/galaxy"" } ]", CardFaces);
            Assert.Throws<InvalidOperationException>(() => noDefault.Validate());

            var definitions = ShopConfiguration.Parse(Items, Gifts, Namecards, CardFaces);
            error = Assert.Throws<InvalidOperationException>(() => definitions.Validate(new[] { "satan-contract" }, new[] { "satan-service" }));
            StringAssert.Contains("satan-contract", error.Message);
            StringAssert.Contains("satan-service", error.Message);

            var reserved = ShopConfiguration.Parse(@"[{ ""Id"": ""namecard-x"", ""Name"": ""X"", ""Icon"": ""x"", ""Kind"": ""Consumable"", ""Price"": 1 }]",
                "[]", Namecards, CardFaces);
            Assert.Throws<InvalidOperationException>(() => reserved.Validate());
        }

        [Test]
        public void CatalogMessageRoundTripsAndRevisionTracksChanges()
        {
            var definitions = ShopConfiguration.Parse(Items, Gifts, Namecards, CardFaces);
            var message = ClientShopCatalog.ToMessage(definitions);
            var copy = ClientShopCatalog.FromMessage(message);
            Assert.AreEqual(definitions.Revision, copy.Revision);
            Assert.AreEqual(message.Revision, definitions.Revision);
            Assert.AreEqual("namecards/devil-service", copy.FindNamecard("devil-service").Artwork);
            Assert.IsTrue(copy.FindNamecard("straight-flush").Reward);
            definitions.Items[1].Price = 60;
            Assert.AreNotEqual(copy.Revision, definitions.Revision);
        }

        [Test]
        public void RepositoryConfigurationIsValid()
        {
            var definitions = ShopConfiguration.Load();
            Assert.AreEqual("default", definitions.Namecards[0].Id);
            Assert.AreEqual("default", definitions.CardFaces[0].Id);
            CollectionAssert.AreEquivalent(new[] { "rotten-egg", "champagne", "gunfire", "swiss-bank-note" }, definitions.Gifts.Select(g => g.ItemId).ToArray());
        }
    }
}
