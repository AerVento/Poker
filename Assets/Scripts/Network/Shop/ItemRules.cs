using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Network.Accounts
{
    /// <summary>Everything the server knows about one item: its definition, optional shop offer and the roles catalogs give it.</summary>
    public sealed class ItemRule
    {
        public ItemDefinition Item { get; }
        public ShopOffer Offer { get; }
        public NamecardDefinition Namecard { get; }
        public CardFaceDefinition CardFace { get; }
        public GiftDefinition Gift { get; }
        public ContractDefinition Contract { get; }
        public ItemPurchasePolicy Policy { get; }
        /// <summary>The Use consume method; null when no catalog binds it.</summary>
        public ItemUsePolicy UsePolicy { get; }

        public string Id => Item.Id;
        public bool Consumable => Item.Consumable;
        public uint MaxQuantity => Item.MaxQuantity;
        public uint Price => Offer?.Price ?? 0;
        /// <summary>Visible in the shop.</summary>
        public bool Listed => Offer?.Listed ?? false;
        public bool CanPurchase => Offer?.CanPurchase ?? false;
        public EquipSlot? Slot => Namecard != null ? EquipSlot.Namecard : CardFace != null ? EquipSlot.CardFace : (EquipSlot?)null;
        /// <summary>The slot's default item, stored as an empty equipped value.</summary>
        public bool IsDefaultAppearance => Namecard?.IsDefault == true || CardFace?.IsDefault == true;

        internal ItemRule(GameCatalog catalog, ItemDefinition item)
        {
            Item = item;
            Offer = catalog.FindOffer(item.Id);
            Namecard = catalog.Namecards.FirstOrDefault(n => n.ItemId == item.Id);
            CardFace = catalog.CardFaces.FirstOrDefault(c => c.ItemId == item.Id);
            Gift = catalog.FindGift(item.Id);
            Contract = catalog.FindContract(item.Id);
            // A contract is signed by buying it and repaid by using it; catalogs bind every other item's behaviour.
            Policy = Contract == null ? new ItemPurchasePolicy() : new ItemPurchasePolicy(
                new IPurchaseCondition[] { new BalanceCondition(ComparisonType.LessThan, Contract.SignBelowAssets) },
                new IPurchaseEffect[] { new RewardChipsEffect(Contract.Payout) });
            UsePolicy = Contract == null ? null : new ItemUsePolicy(
                new IItemUseCondition[] { new UseBalanceCondition(ComparisonType.GreaterThanOrEqual, Contract.Repayment) },
                new IItemUseEffect[] { new PayChipsEffect(Contract.Repayment) },
                ItemUseLocationMask.OutsideRoom | ItemUseLocationMask.WaitingRoom | ItemUseLocationMask.StartedRoom);
        }

        /// <summary>Whether a catalog binds this consume method to the item.</summary>
        public bool Supports(ItemConsumeMethod method) => method switch
        {
            ItemConsumeMethod.Use => UsePolicy != null,
            ItemConsumeMethod.Give => Gift != null,
            _ => false,
        };
    }

    /// <summary>The server rules built from one immutable catalog. A request observes a single configuration.</summary>
    public sealed class ItemRules
    {
        public GameCatalog Catalog { get; }
        public IReadOnlyList<ItemRule> Items { get; }
        /// <summary>Shop offers in Shop.json order.</summary>
        public IReadOnlyList<ItemRule> Offers { get; }
        public string Revision => Catalog.Revision;
        private readonly Dictionary<string, ItemRule> _byId;

        public ItemRules(GameCatalog catalog)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            catalog.Validate();
            var items = catalog.Items.Select(item => new ItemRule(catalog, item)).ToArray();
            Items = Array.AsReadOnly(items);
            _byId = items.ToDictionary(item => item.Id, StringComparer.Ordinal);
            Offers = Array.AsReadOnly(catalog.Shop.Select(offer => _byId[offer.ItemId]).ToArray());
        }

        public ItemRule Find(string itemId) => itemId != null && _byId.TryGetValue(itemId, out var item) ? item : null;

        public static ItemActionResult CheckUse(ItemUseContext context, ItemUseLocation location)
        {
            if (context.Item == null) return ItemActionResult.UnknownItem;
            if (!context.Item.Consumable) return ItemActionResult.NotConsumable;
            var policy = context.Item.UsePolicy;
            if (policy == null) return ItemActionResult.MethodUnavailable;
            if (context.OwnedQuantity == 0) return ItemActionResult.NotOwned;
            if (!policy.Allows(location))
            {
                if ((policy.AllowedLocations & ItemUseLocationMask.StartedRoom) != 0)
                    return ItemActionResult.RoomStartedRequired;
                if ((policy.AllowedLocations & ItemUseLocationMask.OutsideRoom) != 0)
                    return ItemActionResult.OutsideRoomRequired;
                return ItemActionResult.LocationNotAllowed;
            }
            if (policy.Conditions.Any(c => !c.Check(context))) return ItemActionResult.UseConditionNotMet;
            return context.BalanceBefore < policy.Cost ? ItemActionResult.InsufficientDollars : ItemActionResult.Success;
        }

        public static ItemActionResult CheckPurchase(PurchaseContext context, uint quantity,
            out PurchaseConditionDescriptor failedCondition)
        {
            failedCondition = null;
            var rule = context.Item;
            if (rule == null || !rule.CanPurchase) return ItemActionResult.UnknownItem;
            // Several at once only for plain consumables; effects are planned per single purchase.
            if (quantity == 0 || (quantity > 1 && (!rule.Consumable || rule.Policy.OnPurchaseEffects.Count != 0)))
                return ItemActionResult.InvalidRequest;
            if (rule.MaxQuantity == 1 && context.OwnedQuantity > 0) return ItemActionResult.AlreadyOwned;
            if ((ulong)context.OwnedQuantity + quantity > rule.MaxQuantity) return ItemActionResult.QuantityLimit;
            var totalPrice = (ulong)rule.Price * quantity;
            if (totalPrice > context.BalanceBefore) return ItemActionResult.InsufficientDollars;
            failedCondition = rule.Policy.FirstFailure(context);
            if (failedCondition != null) return ItemActionResult.PurchaseConditionNotMet;
            // Known effect descriptions allow read-only range checks; execution checks each actual command again.
            return (ulong)context.BalanceBefore - totalPrice + rule.Policy.RewardChips > uint.MaxValue
                ? ItemActionResult.BalanceLimit : ItemActionResult.Success;
        }
    }
}
