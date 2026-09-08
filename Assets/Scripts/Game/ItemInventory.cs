using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;

namespace Game
{
    public enum ItemKind { Consumable, NonConsumable }
    public enum ItemAction { Purchase, Use }

    // Transport-independent presentation data. Only an authoritative service may change ownership.
    public sealed class ItemStack
    {
        public string ItemId { get; }
        public uint Quantity { get; }

        public ItemStack(string itemId, uint quantity)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException(nameof(itemId));
            ItemId = itemId;
            Quantity = quantity;
        }
    }

    public sealed class ItemOffer
    {
        public string ItemId { get; }
        public uint Price { get; }
        public bool IsListed { get; }
        public uint RewardChips { get; }
        public uint MaxQuantity { get; }
        public IReadOnlyList<PurchaseConditionDescriptor> PurchaseConditions { get; }
        public bool CanPurchase { get; }
        public ItemShopResult BlockReason { get; }

        public ItemOffer(string itemId, uint price, bool isListed = true, uint rewardChips = 0,
            IEnumerable<PurchaseConditionDescriptor> purchaseConditions = null, bool canPurchase = true,
            ItemShopResult blockReason = ItemShopResult.Success, uint maxQuantity = uint.MaxValue)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException(nameof(itemId));
            ItemId = itemId;
            Price = price;
            IsListed = isListed;
            RewardChips = rewardChips;
            MaxQuantity = maxQuantity;
            var conditions = (purchaseConditions ?? Array.Empty<PurchaseConditionDescriptor>()).ToArray();
            if (conditions.Any(c => c == null)) throw new ArgumentException(nameof(purchaseConditions));
            PurchaseConditions = Array.AsReadOnly(conditions);
            CanPurchase = canPurchase;
            BlockReason = blockReason;
        }
    }

    public sealed class ItemInventorySnapshot
    {
        public static ItemInventorySnapshot Empty { get; } = new ItemInventorySnapshot();
        public IReadOnlyList<ItemStack> Items { get; }
        public IReadOnlyList<ItemOffer> Offers { get; }
        public uint Balance { get; }
        public bool CanAct { get; }
        public bool EligibilityCurrent { get; }

        public ItemInventorySnapshot(IEnumerable<ItemStack> items = null, IEnumerable<ItemOffer> offers = null,
            uint balance = 0, bool canAct = false, bool eligibilityCurrent = true)
        {
            var stacks = (items ?? Array.Empty<ItemStack>()).ToArray();
            var products = (offers ?? Array.Empty<ItemOffer>()).ToArray();
            if (stacks.Any(item => item == null) || products.Any(offer => offer == null) ||
                stacks.Select(item => item.ItemId).Distinct(StringComparer.Ordinal).Count() != stacks.Length ||
                products.Select(offer => offer.ItemId).Distinct(StringComparer.Ordinal).Count() != products.Length)
                throw new ArgumentException("Inventory and offers must have unique, non-null entries.");
            Items = Array.AsReadOnly(stacks);
            Offers = Array.AsReadOnly(products);
            Balance = balance;
            CanAct = canAct;
            EligibilityCurrent = eligibilityCurrent;
        }

        public uint QuantityOf(string id) => Items.FirstOrDefault(item => item.ItemId == id)?.Quantity ?? 0;
        public ItemOffer OfferFor(string id) => Offers.FirstOrDefault(offer => offer.ItemId == id && offer.IsListed);
    }

    public sealed class ItemActionRequest
    {
        public string OperationId { get; } = Guid.NewGuid().ToString("N");
        public ItemAction Action { get; }
        public string ItemId { get; }
        public uint Quantity => 1;
        public uint QuotedPrice { get; }

        public ItemActionRequest(ItemAction action, string itemId, uint quotedPrice = 0)
        {
            Action = action;
            ItemId = itemId;
            QuotedPrice = quotedPrice;
        }
    }
}
