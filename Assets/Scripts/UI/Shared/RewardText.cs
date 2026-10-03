using System.Linq;
using Game.Network;

namespace Game.UI
{
    public static class RewardText
    {
        public static string Name(RewardEntry entry)
        {
            if (entry.Kind == RewardItemKind.Dollars) return "美金";
            if (entry.Kind == RewardItemKind.Chips) return "筹码";
            return ItemName(entry.ItemId);
        }

        /// <summary>An item's display name; namecards and card faces add their kind, as in 「星河名片」.</summary>
        public static string ItemName(string itemId)
        {
            var item = ClientCatalog.FindItem(itemId);
            if (item == null) return itemId;
            if (ClientCatalog.Current.FindNamecard(itemId)?.ItemId == itemId) return item.Name + "名片";
            if (ClientCatalog.Current.FindCardFace(itemId)?.ItemId == itemId) return item.Name + "牌面";
            return item.Name;
        }

        public static string Describe(Reward reward, string separator = "、") => string.Join(separator,
            (reward ?? Reward.Empty).Entries.Select(e => e.Kind == RewardItemKind.Item
                ? Name(e) + " ×" + e.Quantity.ToString("N0") : "$" + e.Quantity.ToString("N0") + " " + Name(e)));
    }
}
