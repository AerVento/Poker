using System;
using Game.Network.Accounts;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class NamecardItemIdTest
    {
        [TestCase("galaxy")]
        [TestCase("default")]
        [TestCase("namecard-galaxy")]
        [TestCase("card_01-blue")]
        public void ConversionRoundTripsWithoutChangingLegacyId(string namecardId)
        {
            var itemId = NamecardItemId.ToItemId(namecardId);
            Assert.AreEqual("namecard-" + namecardId, itemId);
            Assert.AreEqual(namecardId, NamecardItemId.ToNamecardId(itemId));
            Assert.IsTrue(NamecardItemId.TryToItemId(namecardId, out var converted));
            Assert.AreEqual(itemId, converted);
            Assert.IsTrue(NamecardItemId.TryToNamecardId(itemId, out converted));
            Assert.AreEqual(namecardId, converted);
        }

        [Test]
        public void PreservesMaximumLengthLegacyIds()
        {
            var legacyId = new string('a', 48);
            var itemId = NamecardItemId.ToItemId(legacyId);
            Assert.AreEqual(NamecardItemId.MaxItemIdLength, itemId.Length);
            Assert.AreEqual(legacyId, NamecardItemId.ToNamecardId(itemId));
            Assert.IsFalse(NamecardItemId.TryToItemId(legacyId + "a", out _));
            Assert.IsFalse(NamecardItemId.TryToNamecardId(itemId + "a", out _));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Galaxy")]
        [TestCase("galaxy blue")]
        [TestCase("../galaxy")]
        public void InvalidLegacyIdsAreRejected(string id)
        {
            Assert.IsFalse(NamecardItemId.TryToItemId(id, out var converted));
            Assert.IsNull(converted);
            Assert.Throws<ArgumentException>(() => NamecardItemId.ToItemId(id));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("galaxy")]
        [TestCase("devil-contract")]
        [TestCase("Namecard-galaxy")]
        [TestCase("namecard-")]
        [TestCase("namecard-Galaxy")]
        public void NonNamecardOrMalformedItemIdsAreRejected(string id)
        {
            Assert.IsFalse(NamecardItemId.TryToNamecardId(id, out var converted));
            Assert.IsNull(converted);
            Assert.Throws<ArgumentException>(() => NamecardItemId.ToNamecardId(id));
        }

        [Test]
        public void RegistryIncludesDelistedNamecardsWithoutMakingThemBackpackItems()
        {
            var namecards = new NamecardServerCatalog { Products = new[]
            {
                new NamecardOffer { Id = "default" },
                new NamecardOffer { Id = "galaxy", Price = 6000, Listed = false }
            }};
            var items = new ItemServerCatalog { Products = new[] { new ServerItemDefinition { Id = "keepsake" } } };
            var registry = new ItemIdentityRegistry(namecards, items);
            Assert.IsTrue(registry.TryGet("namecard-galaxy", out var namecard));
            Assert.AreEqual("galaxy", namecard.NamecardId);
            Assert.AreEqual(ItemKind.NonConsumable, namecard.Kind);
            Assert.AreEqual(1u, namecard.MaxQuantity);
            Assert.IsTrue(namecard.IsEquippable);
            Assert.IsFalse(namecard.ShowInInventory);
            Assert.IsTrue(registry.TryGet("keepsake", out var item));
            Assert.IsTrue(item.ShowInInventory);
            Assert.IsFalse(item.IsEquippable);
            Assert.IsFalse(registry.TryGet("galaxy", out _));
            Assert.IsFalse(registry.TryGet("namecard-missing", out _));
            namecards.Products[1].Id = "renamed";
            Assert.IsTrue(registry.TryGet("namecard-galaxy", out _));
        }

        [Test]
        public void OrdinaryItemsCannotOccupyTheNamecardNamespace()
        {
            var namecards = new NamecardServerCatalog { Products = new[] { new NamecardOffer { Id = "default" } } };
            var items = new ItemServerCatalog { Products = new[] { new ServerItemDefinition { Id = "namecard-future" } } };
            Assert.Throws<InvalidOperationException>(() => new ItemIdentityRegistry(namecards, items));
        }
    }
}
