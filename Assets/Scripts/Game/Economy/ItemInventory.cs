using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;

namespace Game
{
    /// <summary>Every change a player can request for an item. Values are sent on the wire; append only.</summary>
    public enum ItemAction : byte { Purchase, Equip, Use, Give }

    /// <summary>How an item is consumed. Each method is bound to items by a feature catalog.</summary>
    public enum ItemConsumeMethod : byte
    {
        /// <summary>Used from the inventory; bound by Contracts.json.</summary>
        Use = 1,
        /// <summary>Given to another player at a started table; bound by Gifts.json.</summary>
        Give = 2,
    }

    public enum EquipSlot : byte { Namecard, CardFace }

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

    /// <summary>The server's per-account verdict on one shop offer; price and listing come from the catalog.</summary>
    public sealed class ItemOfferState
    {
        public string ItemId { get; }
        public bool CanPurchase { get; }
        public ItemActionResult BlockReason { get; }

        public ItemOfferState(string itemId, bool canPurchase, ItemActionResult blockReason)
        {
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            CanPurchase = canPurchase;
            BlockReason = blockReason;
        }
    }

    /// <summary>An authoritative snapshot of one account's wallet, items and equipment.</summary>
    public sealed class InventorySnapshot
    {
        public static InventorySnapshot Empty { get; } = new InventorySnapshot();
        public IReadOnlyList<ItemStack> Items { get; }
        public uint Dollars { get; }
        public uint AvailableChips { get; }
        public bool CanExchange { get; }
        public string EquippedNamecard { get; }
        public string EquippedCardFace { get; }
        /// <summary>Purchases and uses are allowed.</summary>
        public bool CanAct { get; }
        /// <summary>Equipment can change (outside rooms).</summary>
        public bool CanEquip { get; }
        /// <summary>False after a local wallet change until the server recomputes eligibility.</summary>
        public bool EligibilityCurrent { get; }
        public string CatalogRevision { get; }
        private readonly Dictionary<string, uint> _quantities;
        private readonly Dictionary<string, ItemOfferState> _offers;
        private readonly HashSet<string> _usable;

        public InventorySnapshot(IEnumerable<ItemStack> items = null, IEnumerable<ItemOfferState> offers = null,
            IEnumerable<string> usable = null, uint dollars = 0, uint availableChips = 0, bool canExchange = false,
            string equippedNamecard = null, string equippedCardFace = null, bool canAct = false, bool canEquip = false,
            string catalogRevision = null, bool eligibilityCurrent = true)
        {
            var stacks = (items ?? Array.Empty<ItemStack>()).Where(i => i != null && i.Quantity > 0).ToArray();
            _quantities = new Dictionary<string, uint>(StringComparer.Ordinal);
            foreach (var stack in stacks)
            {
                if (_quantities.ContainsKey(stack.ItemId)) throw new ArgumentException("Inventory items must be unique.");
                _quantities.Add(stack.ItemId, stack.Quantity);
            }
            _offers = new Dictionary<string, ItemOfferState>(StringComparer.Ordinal);
            foreach (var offer in offers ?? Array.Empty<ItemOfferState>())
                if (offer != null) _offers[offer.ItemId] = offer;
            _usable = new HashSet<string>(usable ?? Array.Empty<string>(), StringComparer.Ordinal);
            Items = Array.AsReadOnly(stacks);
            Dollars = dollars;
            AvailableChips = availableChips;
            CanExchange = canExchange;
            EquippedNamecard = equippedNamecard ?? string.Empty;
            EquippedCardFace = equippedCardFace ?? string.Empty;
            CanAct = canAct;
            CanEquip = canEquip;
            CatalogRevision = catalogRevision;
            EligibilityCurrent = eligibilityCurrent;
        }

        public uint QuantityOf(string id) => id != null && _quantities.TryGetValue(id, out var quantity) ? quantity : 0;
        public bool Owns(string id) => QuantityOf(id) > 0;
        public ItemOfferState OfferState(string id) => id != null && _offers.TryGetValue(id, out var offer) ? offer : null;
        public bool CanUse(string id) => id != null && _usable.Contains(id);

        /// <summary>The equipped item of a slot, with an empty value meaning the slot's default.</summary>
        public string Equipped(EquipSlot slot)
        {
            var id = slot == EquipSlot.Namecard ? EquippedNamecard : EquippedCardFace;
            return string.IsNullOrEmpty(id) ? slot == EquipSlot.Namecard ? NamecardDefinition.DefaultItemId : CardFaceDefinition.DefaultItemId : id;
        }

        /// <summary>The same snapshot after a local wallet change; eligibility waits for the server.</summary>
        public InventorySnapshot WithWallet(uint dollars, uint availableChips, bool canExchange) => new InventorySnapshot(
            Items, _offers.Values, _usable, dollars, availableChips, canExchange, EquippedNamecard, EquippedCardFace,
            CanAct, CanEquip, CatalogRevision, false);

        /// <summary>The same snapshot with actions disabled, while a write's outcome is unknown.</summary>
        public InventorySnapshot Locked() => new InventorySnapshot(
            Items, _offers.Values, _usable, Dollars, AvailableChips, CanExchange, EquippedNamecard, EquippedCardFace,
            false, false, CatalogRevision, EligibilityCurrent);
    }

    public sealed class ItemActionRequest
    {
        public string OperationId { get; } = Guid.NewGuid().ToString("N");
        public ItemAction Action { get; }
        public string ItemId { get; }
        public uint Quantity { get; }
        public uint QuotedPrice { get; }

        public ItemActionRequest(ItemAction action, string itemId, uint quotedPrice = 0, uint quantity = 1)
        {
            if (quantity == 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Action = action;
            ItemId = itemId;
            QuotedPrice = quotedPrice;
            Quantity = quantity;
        }
    }
}
