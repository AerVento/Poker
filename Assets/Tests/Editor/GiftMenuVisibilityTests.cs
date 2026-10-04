using System.Collections.Generic;
using System.Linq;
using Game.UI.Gifts;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GiftMenuVisibilityTests
    {
        private static readonly GiftDefinition[] Gifts =
        {
            new GiftDefinition { ItemId = "sold", Effect = "gifts/sold" },
            new GiftDefinition { ItemId = "limited", Effect = "gifts/limited" },
            new GiftDefinition { ItemId = "delisted", Effect = "gifts/delisted" },
            new GiftDefinition { ItemId = "shown-only", Effect = "gifts/shown-only" },
        };

        private static readonly Dictionary<string, ShopOffer> Offers = new Dictionary<string, ShopOffer>
        {
            ["sold"] = new ShopOffer { ItemId = "sold", Price = 100 },
            ["delisted"] = new ShopOffer { ItemId = "delisted", Price = 100, Listed = false },
            ["shown-only"] = new ShopOffer { ItemId = "shown-only", Price = 0, Purchasable = false },
        };

        private static string[] Visible(Dictionary<string, uint> owned) =>
            GiftInteractionController.VisibleGifts(Gifts, owned, id => Offers.TryGetValue(id, out var offer) ? offer : null)
                .Select(g => g.ItemId).ToArray();

        [Test]
        public void WithoutStockOnlyGiftsTheShopSellsAreListed()
        {
            CollectionAssert.AreEqual(new[] { "sold" }, Visible(new Dictionary<string, uint>()));
            CollectionAssert.AreEqual(new[] { "sold" }, Visible(new Dictionary<string, uint> { ["limited"] = 0 }));
        }

        [Test]
        public void OwnedGiftsAreListedInCatalogOrderWhetherOrNotTheShopSellsThem()
        {
            var owned = new Dictionary<string, uint> { ["limited"] = 2, ["delisted"] = 1, ["shown-only"] = 3 };
            CollectionAssert.AreEqual(new[] { "sold", "limited", "delisted", "shown-only" }, Visible(owned));
        }
    }
}
