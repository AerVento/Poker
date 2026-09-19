using System;
using System.Collections.Generic;
using System.IO;
using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class NamecardCatalogLoaderTest
    {
        private static NamecardServerCatalog Defaults() => new NamecardServerCatalog
        {
            Products = new[]
            {
                new NamecardOffer { Id = "default", Price = 0, Listed = true },
                new NamecardOffer { Id = "galaxy", Price = 9000, Listed = true }
            }
        };

        [Test]
        public void PartialOverridePreservesUnspecifiedProductsAndFields()
        {
            var original = Defaults();
            var loaded = NamecardCatalogLoader.ApplyOverrides(original, "[{\"Id\":\"galaxy\",\"Price\":6000}]");

            Assert.AreEqual(2, loaded.Products.Length);
            Assert.AreEqual(0u, loaded.Products[0].Price);
            Assert.AreEqual(6000u, loaded.Products[1].Price);
            Assert.IsTrue(loaded.Products[1].Listed);
            Assert.AreEqual(9000u, original.Products[1].Price);
            Assert.AreNotEqual(original.Revision, loaded.Revision);
        }

        [Test]
        public void UnknownIdIsIgnoredWithWarning()
        {
            var warnings = new List<string>();
            var loaded = NamecardCatalogLoader.ApplyOverrides(Defaults(),
                "[{\"Id\":\"future-namecard\",\"Price\":1}]", warnings.Add);

            Assert.AreEqual(2, loaded.Products.Length);
            Assert.AreEqual(9000u, loaded.Products[1].Price);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("future-namecard", warnings[0]);
        }

        [TestCase("")]
        [TestCase("null")]
        [TestCase("{}")]
        [TestCase("[null]")]
        [TestCase("[{}]")]
        [TestCase("[{\"Id\":1}]")]
        [TestCase("[{\"Id\":\"galaxy\",\"Price\":1,\"Price\":2}]")]
        [TestCase("[{\"Id\":\"galaxy\",\"price\":1}]")]
        public void RejectsMalformedOverrides(string json)
        {
            Assert.Throws<InvalidDataException>(() => NamecardCatalogLoader.ApplyOverrides(Defaults(), json));
        }

        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("4294967296")]
        [TestCase("\"6000\"")]
        public void RejectsInvalidPrices(string price)
        {
            Assert.Throws<InvalidDataException>(() => NamecardCatalogLoader.ApplyOverrides(Defaults(),
                "[{\"Id\":\"galaxy\",\"Price\":" + price + "}]"));
        }

        [Test]
        public void EnforcesDefaultProductAndUniqueOverrideIds()
        {
            Assert.Throws<InvalidOperationException>(() => NamecardCatalogLoader.ApplyOverrides(
                new NamecardServerCatalog { Products = new[] { new NamecardOffer { Id = "default", Price = 1 } } }, "[]"));
            Assert.Throws<InvalidDataException>(() => NamecardCatalogLoader.ApplyOverrides(Defaults(),
                "[{\"Id\":\"galaxy\",\"Price\":1},{\"Id\":\"galaxy\",\"Listed\":false}]"));
            Assert.AreEqual(2, NamecardCatalogLoader.ApplyOverrides(Defaults(), "\uFEFF[]").Products.Length);
        }

        [Test]
        public void OverridesControlPurchasePriceDelistingAndOwnedEquipment()
        {
            var directory = Path.Combine(Path.GetTempPath(), "PokerCatalog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "accounts.db");
            try
            {
                var original = Defaults();
                var repository = new SqliteAccountRepository(path);
                var service = new AccountService(repository, new PasswordHasher(iterations: 1000));
                service.Initialize();
                var owner = service.Register("CatalogOwner", "password123").Account;
                var buyer = service.Register("CatalogBuyer", "password123").Account;
                var discounted = NamecardCatalogLoader.ApplyOverrides(original,
                    "[{\"Id\":\"galaxy\",\"Price\":6000}]");
                Assert.AreEqual(ShopResult.CatalogChanged, repository.ChangeNamecard(owner.AccountId,
                    Guid.NewGuid().ToString("N"), ShopAction.Purchase, "galaxy", original.Revision, discounted));
                Assert.AreEqual(ShopResult.Success, repository.ChangeNamecard(owner.AccountId,
                    Guid.NewGuid().ToString("N"), ShopAction.Purchase, "galaxy", discounted.Revision, discounted));
                Assert.AreEqual(4000u, repository.FindById(owner.AccountId).Chips);

                var delisted = NamecardCatalogLoader.ApplyOverrides(discounted,
                    "[{\"Id\":\"galaxy\",\"Listed\":false}]");
                Assert.AreEqual(ShopResult.UnknownProduct, repository.ChangeNamecard(buyer.AccountId,
                    Guid.NewGuid().ToString("N"), ShopAction.Purchase, "galaxy", delisted.Revision, delisted));
                Assert.AreEqual(ShopResult.Success, repository.ChangeNamecard(owner.AccountId,
                    Guid.NewGuid().ToString("N"), ShopAction.Equip, "galaxy", delisted.Revision, delisted));
                Assert.AreEqual("galaxy", repository.FindById(owner.AccountId).EquippedNamecardId);
                Assert.IsTrue(repository.ReconcileChips().IsConsistent);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
