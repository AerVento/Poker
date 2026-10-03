using System.Collections.Generic;
using System.Linq;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ItemCatalogTests
    {
        private static string Json(string text) => text.Replace('\'', '"');

        private static readonly string Items = Json(@"[
  { 'id': 'egg', 'name': '臭鸡蛋', 'description': '', 'icon': 'egg', 'consumable': true, 'max-quantity': 4294967295 },
  { 'id': 'contract', 'name': '契约', 'description': '', 'icon': 'contract', 'consumable': true, 'max-quantity': 4294967295 },
  { 'id': 'trophy', 'name': '奖杯', 'description': '永久收藏', 'icon': 'trophy', 'consumable': false, 'max-quantity': 1 },
  { 'id': 'namecard-default', 'name': '经典', 'description': '', 'icon': 'namecard-default', 'consumable': false, 'max-quantity': 1 },
  { 'id': 'namecard-gold', 'name': '鎏金', 'description': '', 'icon': 'namecard-gold', 'consumable': false, 'max-quantity': 1 },
  { 'id': 'cardface-default', 'name': '经典牌面', 'description': '', 'icon': 'cardface-default', 'consumable': false, 'max-quantity': 1 }
]");
        private static readonly string Namecards = Json("[ { 'item-id': 'namecard-default', 'artwork': '' }, { 'item-id': 'namecard-gold', 'artwork': 'namecards/gold' } ]");
        private static readonly string CardFaces = Json("[ { 'item-id': 'cardface-default', 'cards': '', 'preview': 'default' } ]");
        private static readonly string Gifts = Json("[ { 'item-id': 'egg', 'effect': 'gifts/egg' } ]");
        private static readonly string Contracts = Json(@"{
  'contracts': [ { 'item-id': 'contract', 'sign-below-assets': 1000, 'payout': 1000, 'repayment': 1100, 'service-years': 60 } ],
  'service-namecards': [ { 'min-service-years': 100, 'namecard-item-id': 'namecard-gold' } ]
}");
        private static readonly string Shop = Json(@"[
  { 'item-id': 'egg', 'price': 50, 'listed': true, 'purchasable': true },
  { 'item-id': 'contract', 'price': 0, 'listed': true, 'purchasable': true },
  { 'item-id': 'namecard-gold', 'price': 100, 'listed': true, 'purchasable': false },
  { 'item-id': 'trophy', 'price': 10, 'listed': false, 'purchasable': true }
]");

        private static GameCatalog Parse(out List<string> errors, string items = null, string namecards = null, string cardFaces = null,
            string gifts = null, string contracts = null, string shop = null)
        {
            errors = new List<string>();
            var catalog = CatalogConfiguration.Parse(items ?? Items, namecards ?? Namecards, cardFaces ?? CardFaces, gifts ?? Gifts,
                contracts ?? Contracts, shop ?? Shop, errors);
            if (errors.Count == 0) errors.AddRange(catalog.GetErrors());
            return catalog;
        }

        private static void AssertError(string fragment, List<string> errors) =>
            Assert.IsTrue(errors.Any(e => e.Contains(fragment)), "Expected an error containing '" + fragment + "', got:\n" + string.Join("\n", errors));

        [Test]
        public void ValidCatalogSeparatesItemsRolesAndOffers()
        {
            var catalog = Parse(out var errors);
            CollectionAssert.IsEmpty(errors);
            Assert.IsTrue(catalog.FindItem("egg").Consumable);
            Assert.AreEqual(uint.MaxValue, catalog.FindItem("egg").MaxQuantity);
            Assert.IsFalse(catalog.FindItem("trophy").Consumable);
            Assert.AreEqual(1u, catalog.FindItem("trophy").MaxQuantity);
            Assert.AreEqual("namecards/gold", catalog.FindNamecard("namecard-gold").Artwork);
            Assert.AreEqual(NamecardDefinition.DefaultItemId, catalog.FindNamecard(string.Empty).ItemId);
            Assert.IsFalse(catalog.FindOffer("namecard-gold").CanPurchase);
            Assert.IsTrue(catalog.FindOffer("namecard-gold").Listed);
            Assert.IsFalse(catalog.FindOffer("trophy").Listed);
            Assert.IsNull(catalog.FindOffer("namecard-default"));
            Assert.AreEqual(catalog.Revision, Parse(out _).Revision);
            Assert.AreNotEqual(catalog.Revision, Parse(out _, shop: Shop.Replace("50", "60")).Revision);
        }

        [Test]
        public void RolesRequireMatchingItems()
        {
            Parse(out var errors, namecards: Json("[ { 'item-id': 'namecard-default', 'artwork': '' }, { 'item-id': 'namecard-gold', 'artwork': 'a' }, { 'item-id': 'egg', 'artwork': 'b' } ]"));
            AssertError("namecard item IDs start with namecard-", errors);
            AssertError("Gifts.json egg: already used as a namecard", errors);

            Parse(out errors, gifts: Json("[ { 'item-id': 'trophy', 'effect': 'gifts/trophy' } ]"));
            AssertError("gifts must be consumable", errors);

            Parse(out errors, gifts: Json("[ { 'item-id': 'contract', 'effect': 'gifts/c' } ]"));
            AssertError("Contracts.json contract: already used as a gift", errors);

            Parse(out errors, items: Items.Replace(Json("'icon': 'namecard-gold', 'consumable': false"), Json("'icon': 'namecard-gold', 'consumable': true")));
            AssertError("namecards cannot be consumable", errors);

            Parse(out errors, namecards: Json("[ { 'item-id': 'namecard-default', 'artwork': '' } ]"),
                contracts: Json("{ 'contracts': [], 'service-namecards': [] }"));
            AssertError("must be listed in Namecards.json", errors);

            Parse(out errors, shop: Json("[ { 'item-id': 'ghost', 'price': 1, 'listed': true, 'purchasable': true } ]"));
            AssertError("Shop.json ghost: must reference an item", errors);
        }

        [Test]
        public void EveryProblemIsReportedTogether()
        {
            Parse(out var errors,
                items: Items.Replace("'icon': 'trophy'".Replace('\'', '"'), "'icon': 'trophy', 'kind': 'Consumable'".Replace('\'', '"')),
                namecards: Json("[ { 'item-id': 'namecard-gold', 'artwork': 'x' } ]"),
                shop: Json("[ { 'item-id': 'egg', 'price': -1, 'listed': true, 'purchasable': true } ]"));
            AssertError("unknown field kind", errors);
            AssertError("Shop.json entry 0 field price", errors);
            Assert.GreaterOrEqual(errors.Count, 2);

            Parse(out errors, namecards: Json("[ { 'item-id': 'namecard-gold', 'artwork': 'x' } ]"));
            AssertError("must define namecard-default", errors);

            Parse(out errors, contracts: Json("{ 'contracts': [ { 'item-id': 'contract', 'sign-below-assets': 0, 'payout': 0, 'repayment': 1, 'service-years': 1 } ], 'service-namecards': [] }"));
            AssertError("sign-below-assets must be positive", errors);
            AssertError("payout must be positive", errors);

            Parse(out errors, gifts: "{ not json");
            AssertError("Gifts.json: invalid JSON", errors);
        }

        [Test]
        public void EveryFieldIsRequired()
        {
            Parse(out var errors, shop: Json("[ { 'item-id': 'egg', 'price': 50 } ]"),
                cardFaces: Json("[ { 'item-id': 'cardface-default', 'preview': 'default' } ]"));
            AssertError("Shop.json entry 0 field listed: is required", errors);
            AssertError("Shop.json entry 0 field purchasable: is required", errors);
            AssertError("CardFaces.json entry 0 field cards: is required", errors);

            Parse(out errors, items: Items.Replace(Json(", 'max-quantity': 1 }"), " }"));
            AssertError("field max-quantity: is required", errors);
        }

        [Test]
        public void ContractsDriveServiceYearsDebtAndNamecards()
        {
            var catalog = Parse(out _);
            var held = new[] { new ItemStack("contract", 2), new ItemStack("egg", 5) };
            Assert.AreEqual(120UL, catalog.ServiceYears(held));
            Assert.AreEqual(2000UL, catalog.ContractDebt(held));
            Assert.IsTrue(catalog.IsServing(120));
            Assert.IsFalse(catalog.IsServing(60));
            Assert.AreEqual("namecard-gold", catalog.ResolveNamecard("namecard-default", 120));
            Assert.AreEqual("namecard-default", catalog.ResolveNamecard("namecard-default", 60));
        }

        [Test]
        public void CheckedInConfigurationIsValid()
        {
            var configuration = ServerConfiguration.Load();
            var catalog = configuration.Catalog;
            // Every namecard and card face is an item; no fixed counts, the catalog grows over time.
            Assert.IsTrue(catalog.Namecards.All(n => catalog.FindItem(n.ItemId) != null) && catalog.CardFaces.All(c => catalog.FindItem(c.ItemId) != null));
            foreach (var id in new[] { "devil-contract", "archdevil-contract", "satan-contract" })
                Assert.IsNotNull(catalog.FindContract(id));
            Assert.AreEqual(1100u, catalog.FindContract("devil-contract").Repayment);
            Assert.AreEqual("namecard-satan-service", catalog.ResolveNamecard(string.Empty, 1000));
            Assert.AreEqual(10000UL, configuration.StarterGrants.Dollars);
            CollectionAssert.AreEquivalent(new[] { "namecard-default", "cardface-default", "cardface-minimal" },
                configuration.StarterGrants.Items.Select(i => i.ItemId));
        }

        [Test]
        public void CatalogMessageRoundTrips()
        {
            var catalog = Parse(out _);
            var copy = ClientCatalog.FromMessage(ClientCatalog.ToMessage(catalog));
            Assert.AreEqual(catalog.Revision, copy.Revision);
            Assert.AreEqual(catalog.Items.Length, copy.Items.Length);
            Assert.AreEqual(1100u, copy.FindContract("contract").Repayment);
        }

        [Test]
        public void EmptyFilesAreReported()
        {
            var errors = new List<string>();
            CatalogConfiguration.ReadArray("Items.json", "", errors).ToArray();
            AssertError("Items.json: is empty", errors);
        }
    }
}
