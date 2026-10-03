using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game
{
    /// <summary>
    /// One item, configured in Configs/Items/Items.json. Every ownable thing is an item; the feature catalogs
    /// (namecards, card faces, gifts, contracts) only add roles and art to items listed here.
    /// </summary>
    public sealed class ItemDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        /// <summary>Sprite name in the shared remote item icon set.</summary>
        public string Icon { get; set; }
        /// <summary>Only consumable items can be consumed, and only by a method a feature catalog binds to them.</summary>
        public bool Consumable { get; set; }
        public uint MaxQuantity { get; set; } = 1;
    }

    /// <summary>Makes an item a namecard. Configured in Configs/Items/Namecards.json.</summary>
    public sealed class NamecardDefinition
    {
        public const string Prefix = "namecard-";
        public const string DefaultItemId = "namecard-default";
        public string ItemId { get; set; }
        /// <summary>Remote content address of the artwork; empty only for the local default.</summary>
        public string Artwork { get; set; } = string.Empty;
        public bool IsDefault => ItemId == DefaultItemId;
    }

    /// <summary>Makes an item a card face. Configured in Configs/Items/CardFaces.json.</summary>
    public sealed class CardFaceDefinition
    {
        public const string Prefix = "cardface-";
        public const string DefaultItemId = "cardface-default";
        public string ItemId { get; set; }
        /// <summary>Remote content address of the 52 faces and back; empty only for the local classic deck.</summary>
        public string Cards { get; set; } = string.Empty;
        /// <summary>Sprite name in the shared remote preview set.</summary>
        public string Preview { get; set; }
        public bool IsDefault => ItemId == DefaultItemId;
    }

    /// <summary>Lets a consumable item be given at the table. Configured in Configs/Items/Gifts.json; array order is menu order.</summary>
    public sealed class GiftDefinition
    {
        public string ItemId { get; set; }
        /// <summary>Remote content address of the gift's effect bundle, such as gifts/rotten-egg.</summary>
        public string Effect { get; set; }
    }

    /// <summary>Makes a consumable item a devil contract that can be used to repay it. Configured in Configs/Items/Contracts.json.</summary>
    public sealed class ContractDefinition
    {
        public string ItemId { get; set; }
        /// <summary>Signing (buying) is allowed only while total assets are below this amount.</summary>
        public uint SignBelowAssets { get; set; }
        /// <summary>Dollars credited when signed; also the debt each held contract represents.</summary>
        public uint Payout { get; set; }
        /// <summary>Dollars paid to use (repay) one contract.</summary>
        public uint Repayment { get; set; }
        /// <summary>Years of service each held contract adds.</summary>
        public uint ServiceYears { get; set; }
    }

    /// <summary>A namecard forced on players whose service reaches a threshold. Configured in Configs/Items/Contracts.json.</summary>
    public sealed class ServiceNamecardDefinition
    {
        public ulong MinServiceYears { get; set; }
        public string NamecardItemId { get; set; }
    }

    /// <summary>An item the shop may sell. Configured in Configs/Shop/Shop.json; array order is shop order.</summary>
    public sealed class ShopOffer
    {
        public string ItemId { get; set; }
        public uint Price { get; set; }
        /// <summary>False delists the offer without removing it from the file.</summary>
        public bool Listed { get; set; } = true;
        /// <summary>False shows a listed offer with a disabled purchase button.</summary>
        public bool Purchasable { get; set; } = true;
        public bool CanPurchase => Listed && Purchasable;
    }

    /// <summary>
    /// The authoritative item and shop catalogs. The server loads them from Configs and sends them to clients,
    /// which read every item name, role, art address and price from here.
    /// </summary>
    public sealed class GameCatalog
    {
        public const int MaxEntries = 1000;
        public const int MaxIdLength = 64;

        public ItemDefinition[] Items { get; set; } = Array.Empty<ItemDefinition>();
        public NamecardDefinition[] Namecards { get; set; } = Array.Empty<NamecardDefinition>();
        public CardFaceDefinition[] CardFaces { get; set; } = Array.Empty<CardFaceDefinition>();
        public GiftDefinition[] Gifts { get; set; } = Array.Empty<GiftDefinition>();
        public ContractDefinition[] Contracts { get; set; } = Array.Empty<ContractDefinition>();
        public ServiceNamecardDefinition[] ServiceNamecards { get; set; } = Array.Empty<ServiceNamecardDefinition>();
        public ShopOffer[] Shop { get; set; } = Array.Empty<ShopOffer>();

        private Dictionary<string, ItemDefinition> _items;
        private Dictionary<string, NamecardDefinition> _namecards;
        private Dictionary<string, CardFaceDefinition> _cardFaces;
        private Dictionary<string, GiftDefinition> _gifts;
        private Dictionary<string, ContractDefinition> _contracts;
        private Dictionary<string, ShopOffer> _offers;
        private string _revision;

        public ItemDefinition FindItem(string id) => Find(ref _items, Items, i => i.Id, id);
        public NamecardDefinition FindNamecard(string itemId) =>
            Find(ref _namecards, Namecards, n => n.ItemId, string.IsNullOrEmpty(itemId) ? NamecardDefinition.DefaultItemId : itemId);
        public CardFaceDefinition FindCardFace(string itemId) =>
            Find(ref _cardFaces, CardFaces, c => c.ItemId, string.IsNullOrEmpty(itemId) ? CardFaceDefinition.DefaultItemId : itemId);
        public GiftDefinition FindGift(string itemId) => Find(ref _gifts, Gifts, g => g.ItemId, itemId);
        public ContractDefinition FindContract(string itemId) => Find(ref _contracts, Contracts, c => c.ItemId, itemId);
        public ShopOffer FindOffer(string itemId) => Find(ref _offers, Shop, o => o.ItemId, itemId);

        /// <summary>Holding limit of a known item; null for an unknown ID.</summary>
        public uint? MaxQuantityOf(string itemId) => FindItem(itemId)?.MaxQuantity;

        /// <summary>Total years of service from held contracts.</summary>
        public ulong ServiceYears(Func<string, uint> quantityOf)
        {
            ulong years = 0;
            foreach (var contract in Contracts) years = checked(years + (ulong)quantityOf(contract.ItemId) * contract.ServiceYears);
            return years;
        }

        public ulong ServiceYears(IEnumerable<ItemStack> items)
        {
            var stacks = (items ?? Array.Empty<ItemStack>()).ToDictionary(i => i.ItemId, i => i.Quantity, StringComparer.Ordinal);
            return ServiceYears(id => stacks.TryGetValue(id, out var quantity) ? quantity : 0u);
        }

        /// <summary>Dollars owed for held contracts: each one is worth its payout.</summary>
        public ulong ContractDebt(IEnumerable<ItemStack> items)
        {
            ulong debt = 0;
            foreach (var stack in items ?? Array.Empty<ItemStack>())
            {
                var contract = FindContract(stack.ItemId);
                if (contract != null) debt = checked(debt + (ulong)stack.Quantity * contract.Payout);
            }
            return debt;
        }

        /// <summary>Players serving at least the lowest service-namecard threshold cannot pick their namecard or send gifts.</summary>
        public bool IsServing(ulong serviceYears) => ServiceNamecards.Length > 0 && serviceYears >= ServiceNamecards.Min(s => s.MinServiceYears);

        /// <summary>The namecard others see: the highest reached service namecard, otherwise the equipped one.</summary>
        public string ResolveNamecard(string equippedItemId, ulong serviceYears)
        {
            ServiceNamecardDefinition reached = null;
            foreach (var service in ServiceNamecards)
                if (serviceYears >= service.MinServiceYears && (reached == null || service.MinServiceYears > reached.MinServiceYears))
                    reached = service;
            return reached?.NamecardItemId ?? equippedItemId;
        }

        /// <summary>Changes whenever any definition changes; clients and purchase requests compare it.</summary>
        public string Revision
        {
            get
            {
                if (_revision != null) return _revision;
                var text = JsonSerializer.Serialize(new { Items, Namecards, CardFaces, Gifts, Contracts, ServiceNamecards, Shop });
                using var sha = SHA256.Create();
                return _revision = string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Take(12).Select(b => b.ToString("x2")));
            }
        }

        /// <summary>Throws with every problem found; a server must not start with an invalid catalog.</summary>
        public void Validate() => ThrowIfAny(GetErrors());

        public static void ThrowIfAny(IReadOnlyCollection<string> errors)
        {
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }

        public List<string> GetErrors()
        {
            var errors = new List<string>();
            void Check(bool condition, string message) { if (!condition) errors.Add(message); }
            if (Items == null || Namecards == null || CardFaces == null || Gifts == null || Contracts == null ||
                ServiceNamecards == null || Shop == null)
            {
                errors.Add("Every catalog list is required.");
                return errors;
            }
            foreach (var (name, count) in new[] { ("Items.json", Items.Length), ("Namecards.json", Namecards.Length),
                ("CardFaces.json", CardFaces.Length), ("Gifts.json", Gifts.Length), ("Contracts.json", Contracts.Length),
                ("Shop.json", Shop.Length) })
                Check(count <= MaxEntries, name + " holds at most " + MaxEntries + " entries.");

            var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (var item in Items)
            {
                if (item == null) { errors.Add("Items.json has an empty entry."); continue; }
                var label = "Items.json " + item.Id + ": ";
                Check(IsValidId(item.Id), label + "ID must be 1-" + MaxIdLength + " lowercase letters, digits, '_' or '-'.");
                Check(item.Id == null || !items.ContainsKey(item.Id), label + "duplicate ID.");
                if (item.Id != null) items[item.Id] = item;
                Check(!string.IsNullOrWhiteSpace(item.Name), label + "name is required.");
                Check(!string.IsNullOrWhiteSpace(item.Icon), label + "icon is required.");
                Check(item.MaxQuantity > 0 && (item.Consumable || item.MaxQuantity == 1),
                    label + "max-quantity must be positive, and 1 for non-consumable items.");
            }

            // Each item plays at most one role, and the namecard-/cardface- prefixes always mark their role.
            var roles = new Dictionary<string, string>(StringComparer.Ordinal);
            ItemDefinition Role(string file, string itemId, string role, Func<ItemDefinition, string> requirement)
            {
                var label = file + " " + itemId + ": ";
                if (itemId == null || !items.TryGetValue(itemId, out var item))
                {
                    errors.Add(label + "must reference an item in Items.json.");
                    return null;
                }
                if (roles.TryGetValue(itemId, out var existing))
                    errors.Add(label + (existing == role ? "listed twice." : "already used as a " + existing + "."));
                else roles[itemId] = role;
                var problem = requirement(item);
                if (problem != null) errors.Add(label + problem);
                return item;
            }

            foreach (var card in Namecards)
            {
                if (card == null) { errors.Add("Namecards.json has an empty entry."); continue; }
                Role("Namecards.json", card.ItemId, "namecard", item => !card.ItemId.StartsWith(NamecardDefinition.Prefix, StringComparison.Ordinal)
                    ? "namecard item IDs start with " + NamecardDefinition.Prefix + "." : item.Consumable ? "namecards cannot be consumable." : null);
                Check(card.IsDefault ? string.IsNullOrEmpty(card.Artwork) : !string.IsNullOrWhiteSpace(card.Artwork),
                    "Namecards.json " + card.ItemId + ": " + (card.IsDefault ? "the default namecard uses local art; leave artwork empty." : "artwork address is required."));
            }
            Check(Namecards.Any(c => c?.IsDefault == true), "Namecards.json must define " + NamecardDefinition.DefaultItemId + ".");

            foreach (var face in CardFaces)
            {
                if (face == null) { errors.Add("CardFaces.json has an empty entry."); continue; }
                Role("CardFaces.json", face.ItemId, "card face", item => !face.ItemId.StartsWith(CardFaceDefinition.Prefix, StringComparison.Ordinal)
                    ? "card face item IDs start with " + CardFaceDefinition.Prefix + "." : item.Consumable ? "card faces cannot be consumable." : null);
                var label = "CardFaces.json " + face.ItemId + ": ";
                Check(!string.IsNullOrWhiteSpace(face.Preview), label + "preview is required.");
                Check(face.IsDefault ? string.IsNullOrEmpty(face.Cards) : !string.IsNullOrWhiteSpace(face.Cards),
                    label + (face.IsDefault ? "the classic deck is local; leave cards empty." : "cards address is required."));
            }
            Check(CardFaces.Any(c => c?.IsDefault == true), "CardFaces.json must define " + CardFaceDefinition.DefaultItemId + ".");

            foreach (var item in Items.Where(i => i?.Id != null))
            {
                if (item.Id.StartsWith(NamecardDefinition.Prefix, StringComparison.Ordinal) && !Namecards.Any(c => c?.ItemId == item.Id))
                    errors.Add("Items.json " + item.Id + ": items with the " + NamecardDefinition.Prefix + " prefix must be listed in Namecards.json.");
                if (item.Id.StartsWith(CardFaceDefinition.Prefix, StringComparison.Ordinal) && !CardFaces.Any(c => c?.ItemId == item.Id))
                    errors.Add("Items.json " + item.Id + ": items with the " + CardFaceDefinition.Prefix + " prefix must be listed in CardFaces.json.");
            }

            foreach (var gift in Gifts)
            {
                if (gift == null) { errors.Add("Gifts.json has an empty entry."); continue; }
                Role("Gifts.json", gift.ItemId, "gift", item => item.Consumable ? null : "gifts must be consumable items.");
                Check(!string.IsNullOrWhiteSpace(gift.Effect), "Gifts.json " + gift.ItemId + ": effect address is required.");
            }

            foreach (var contract in Contracts)
            {
                if (contract == null) { errors.Add("Contracts.json has an empty contract."); continue; }
                Role("Contracts.json", contract.ItemId, "contract", item => item.Consumable ? null : "contracts must be consumable items.");
                var label = "Contracts.json " + contract.ItemId + ": ";
                Check(contract.SignBelowAssets > 0, label + "sign-below-assets must be positive.");
                Check(contract.Payout > 0, label + "payout must be positive.");
                Check(contract.Repayment > 0, label + "repayment must be positive.");
                Check(contract.ServiceYears > 0, label + "service-years must be positive.");
            }
            var thresholds = new HashSet<ulong>();
            foreach (var service in ServiceNamecards)
            {
                if (service == null) { errors.Add("Contracts.json has an empty service namecard."); continue; }
                var label = "Contracts.json service namecard " + service.NamecardItemId + ": ";
                Check(service.MinServiceYears > 0, label + "min-service-years must be positive.");
                Check(thresholds.Add(service.MinServiceYears), label + "duplicate min-service-years.");
                Check(service.NamecardItemId != null && Namecards.Any(c => c?.ItemId == service.NamecardItemId && !c.IsDefault),
                    label + "must reference a non-default namecard in Namecards.json.");
            }

            var offers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var offer in Shop)
            {
                if (offer == null) { errors.Add("Shop.json has an empty entry."); continue; }
                var label = "Shop.json " + offer.ItemId + ": ";
                Check(offer.ItemId != null && items.ContainsKey(offer.ItemId), label + "must reference an item in Items.json.");
                Check(offer.ItemId == null || offers.Add(offer.ItemId), label + "listed twice.");
            }
            return errors;
        }

        public static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= MaxIdLength &&
            id.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-');

        private static T Find<T>(ref Dictionary<string, T> index, T[] values, Func<T, string> key, string id) where T : class
        {
            if (id == null) return null;
            if (index == null)
            {
                index = new Dictionary<string, T>(StringComparer.Ordinal);
                foreach (var value in values ?? Array.Empty<T>())
                    if (value != null && key(value) != null && !index.ContainsKey(key(value))) index.Add(key(value), value);
            }
            return index.TryGetValue(id, out var found) ? found : null;
        }
    }
}
