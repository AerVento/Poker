using System;
using System.IO;
using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class NamecardCatalogLoaderTest
    {
        private const string DefaultJson = "[{\"Id\":\"default\",\"Price\":0,\"Listed\":true}," +
            "{\"Id\":\"galaxy\",\"Price\":9000,\"Listed\":true}]";
        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "PokerCatalog-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "Configs", NamecardCatalogLoader.FileName);
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void FirstLoadCreatesFileAndNextLoadPreservesOperatorEdits()
        {
            var original = NamecardCatalogLoader.LoadOrCreate(_path, () => DefaultJson);
            Assert.AreEqual(DefaultJson, File.ReadAllText(_path));
            var edited = DefaultJson.Replace("9000", "6000").Replace("6000,\"Listed\":true", "6000,\"Listed\":false");
            File.WriteAllText(_path, edited);
            var loaded = NamecardCatalogLoader.LoadOrCreate(_path, () => throw new Exception("Must not read embedded defaults."));
            Assert.AreEqual(6000u, loaded.Products[1].Price);
            Assert.IsFalse(loaded.Products[1].Listed);
            Assert.AreNotEqual(original.Revision, loaded.Revision);
            Assert.AreEqual(edited, File.ReadAllText(_path));
        }

        [Test]
        public void InvalidExternalFileFailsWithPathAndIsNotReplaced()
        {
            NamecardCatalogLoader.LoadOrCreate(_path, () => DefaultJson);
            File.WriteAllText(_path, "broken json");
            var error = Assert.Throws<InvalidDataException>(() =>
                NamecardCatalogLoader.LoadOrCreate(_path, () => throw new Exception("Must not fall back.")));
            StringAssert.Contains(_path, error.Message);
            Assert.AreEqual("broken json", File.ReadAllText(_path));
        }

        [Test]
        public void InvalidDefaultsDoNotCreateAnExternalFile()
        {
            Assert.Throws<InvalidDataException>(() => NamecardCatalogLoader.LoadOrCreate(_path, () => "[]"));
            Assert.IsFalse(File.Exists(_path));
        }

        [TestCase("")]
        [TestCase("null")]
        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("[null]")]
        [TestCase("[{\"Id\":\"default\",\"Listed\":true}]")]
        [TestCase("[{\"Id\":\"default\",\"Price\":0}]")]
        [TestCase("[{\"Id\":\"default\",\"Price\":0,\"Listed\":\"true\"}]")]
        [TestCase("[{\"Id\":\"default\",\"Price\":0,\"Listed\":true,\"Price\":1}]")]
        [TestCase("[{\"Id\":\"default\",\"Price\":0,\"Listed\":true,\"price\":0}]")]
        public void RejectsMalformedOrIncompleteCatalog(string json)
        {
            Assert.Throws<InvalidDataException>(() => NamecardCatalogLoader.Parse(json));
        }

        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("4294967296")]
        [TestCase("\"6000\"")]
        public void RejectsInvalidPrices(string price)
        {
            Assert.Throws<InvalidDataException>(() => NamecardCatalogLoader.Parse(DefaultJson.Replace("9000", price)));
        }

        [Test]
        public void EnforcesDefaultAndUniqueStableIds()
        {
            Assert.Throws<InvalidDataException>(() => NamecardCatalogLoader.Parse(DefaultJson.Replace("\"Price\":0", "\"Price\":1")));
            Assert.Throws<InvalidDataException>(() => NamecardCatalogLoader.Parse(DefaultJson.Replace("true", "false")));
            Assert.Throws<InvalidDataException>(() => NamecardCatalogLoader.Parse(DefaultJson.Replace("galaxy", "default")));
            Assert.Throws<InvalidDataException>(() => NamecardCatalogLoader.Parse(DefaultJson.Replace("galaxy", "Invalid ID")));
            Assert.AreEqual(2, NamecardCatalogLoader.Parse("\uFEFF" + DefaultJson).Products.Length);
        }

        [Test]
        public void ReloadedFileControlsPurchasePriceDelistingAndOwnedEquipment()
        {
            var original = NamecardCatalogLoader.LoadOrCreate(_path, () => DefaultJson);
            var repository = new SqliteAccountRepository(Path.Combine(_directory, "accounts.db"));
            var service = new AccountService(repository, new PasswordHasher(iterations: 1000));
            service.Initialize();
            var owner = service.Register("CatalogOwner", "password123").Account;
            var buyer = service.Register("CatalogBuyer", "password123").Account;
            File.WriteAllText(_path, DefaultJson.Replace("9000", "6000"));
            var discounted = NamecardCatalogLoader.LoadOrCreate(_path, () => DefaultJson);
            Assert.AreEqual(ShopResult.CatalogChanged, repository.ChangeNamecard(owner.AccountId,
                Guid.NewGuid().ToString("N"), ShopAction.Purchase, "galaxy", original.Revision, discounted));
            Assert.AreEqual(10000u, repository.FindById(owner.AccountId).Chips);
            Assert.AreEqual(ShopResult.Success, repository.ChangeNamecard(owner.AccountId,
                Guid.NewGuid().ToString("N"), ShopAction.Purchase, "galaxy", discounted.Revision, discounted));
            Assert.AreEqual(4000u, repository.FindById(owner.AccountId).Chips);

            File.WriteAllText(_path, DefaultJson.Replace("9000,\"Listed\":true", "6000,\"Listed\":false"));
            var delisted = NamecardCatalogLoader.LoadOrCreate(_path, () => DefaultJson);
            Assert.AreEqual(ShopResult.UnknownProduct, repository.ChangeNamecard(buyer.AccountId,
                Guid.NewGuid().ToString("N"), ShopAction.Purchase, "galaxy", delisted.Revision, delisted));
            Assert.AreEqual(10000u, repository.FindById(buyer.AccountId).Chips);
            Assert.AreEqual(ShopResult.Success, repository.ChangeNamecard(owner.AccountId,
                Guid.NewGuid().ToString("N"), ShopAction.Equip, "galaxy", delisted.Revision, delisted));
            Assert.AreEqual("galaxy", repository.FindById(owner.AccountId).EquippedNamecardId);
            Assert.IsTrue(repository.ReconcileChips().IsConsistent);
        }
    }
}
