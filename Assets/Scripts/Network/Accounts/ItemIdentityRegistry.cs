using System;
using System.Collections.Generic;

namespace Game.Network.Accounts
{
    public sealed class ItemIdentity
    {
        public string ItemId { get; }
        public string NamecardId { get; }
        public ItemKind Kind { get; }
        public uint MaxQuantity { get; }
        public bool IsNamecard => NamecardId != null;
        public bool IsEquippable => IsNamecard;
        public bool ShowInInventory => !IsNamecard;

        internal ItemIdentity(string itemId, ItemKind kind, uint maxQuantity, string namecardId = null)
        {
            ItemId = itemId;
            Kind = kind;
            MaxQuantity = maxQuantity;
            NamecardId = namecardId;
        }
    }

    // Identity/ownership routing only. This registry never becomes a shop's list of offers.
    public sealed class ItemIdentityRegistry
    {
        private readonly Dictionary<string, ItemIdentity> _items = new Dictionary<string, ItemIdentity>(StringComparer.Ordinal);
        public IReadOnlyCollection<ItemIdentity> Items { get; }

        public ItemIdentityRegistry(NamecardServerCatalog namecards, ItemServerCatalog inventoryItems)
        {
            if (namecards == null || inventoryItems == null)
                throw new ArgumentNullException();
            namecards.Validate();
            inventoryItems.Validate();
            foreach (var product in inventoryItems.Products)
            {
                // Reserve the entire namespace, including IDs for namecards added in a future release.
                if (product.Id.StartsWith(NamecardItemId.Prefix, StringComparison.Ordinal))
                    throw new InvalidOperationException("Ordinary items cannot use the reserved namecard- prefix.");
                _items.Add(product.Id, new ItemIdentity(product.Id, product.Kind, product.MaxQuantity));
            }
            foreach (var product in namecards.Products)
            {
                var itemId = NamecardItemId.ToItemId(product.Id);
                _items.Add(itemId, new ItemIdentity(itemId, ItemKind.NonConsumable, 1, product.Id));
            }
            Items = new List<ItemIdentity>(_items.Values).AsReadOnly();
        }

        public bool TryGet(string itemId, out ItemIdentity identity)
        {
            identity = null;
            return itemId != null && _items.TryGetValue(itemId, out identity);
        }
    }
}
