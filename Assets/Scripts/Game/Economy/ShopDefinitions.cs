using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Game
{
    /// <summary>One inventory item: what players see and the default offer. Configured in Configs/Shop/Items.json.</summary>
    public sealed class ItemDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>Sprite name in the shared remote item icon set.</summary>
        public string Icon { get; set; }
        public ItemKind Kind { get; set; } = ItemKind.Consumable;
        public uint Price { get; set; }
        public bool Listed { get; set; } = true;
        public uint MaxQuantity { get; set; } = uint.MaxValue;
    }

    /// <summary>An item usable as a gift at the table. Configured in Configs/Shop/Gifts.json; array order is menu order.</summary>
    public sealed class GiftDefinition
    {
        public string ItemId { get; set; }
        /// <summary>Remote content address of the gift's effect bundle, such as gifts/rotten-egg.</summary>
        public string Effect { get; set; }
    }

    /// <summary>A namecard. The first entry is the free local default. Configured in Configs/Shop/Namecards.json.</summary>
    public sealed class NamecardDefinition
    {
        public const string DefaultId = "default";
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>Remote content address of the artwork; empty only for the local default.</summary>
        public string Artwork { get; set; }
        public uint Price { get; set; }
        /// <summary>Visible to players who do not own it.</summary>
        public bool Listed { get; set; } = true;
        /// <summary>When false a listed card shows a disabled purchase button.</summary>
        public bool Purchasable { get; set; } = true;
        /// <summary>Only granted (achievements); never sold.</summary>
        public bool Reward { get; set; }
        public bool IsDefault => Id == DefaultId;
        /// <summary>Shows an enabled purchase button.</summary>
        public bool CanPurchase => IsDefault || Purchasable && !Reward;
        public bool Sellable => IsDefault || Listed && CanPurchase;
    }

    /// <summary>A card face. The first entry is the free local classic deck. Configured in Configs/Shop/CardFaces.json.</summary>
    public sealed class CardFaceDefinition
    {
        public const string DefaultId = "default";
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>Remote content address of the 52 faces and back; empty only for the local classic deck.</summary>
        public string Cards { get; set; }
        /// <summary>Sprite name in the shared remote preview set.</summary>
        public string Preview { get; set; }
        public uint Price { get; set; }
        public bool Listed { get; set; } = true;
        public bool IsDefault => Id == DefaultId;
    }

    /// <summary>The authoritative product lists. The server loads them from Configs/Shop and sends them to clients.</summary>
    public sealed class ShopDefinitions
    {
        public const int MaxProducts = 500;
        public ItemDefinition[] Items { get; set; } = Array.Empty<ItemDefinition>();
        public GiftDefinition[] Gifts { get; set; } = Array.Empty<GiftDefinition>();
        public NamecardDefinition[] Namecards { get; set; } = Array.Empty<NamecardDefinition>();
        public CardFaceDefinition[] CardFaces { get; set; } = Array.Empty<CardFaceDefinition>();

        public ItemDefinition FindItem(string id) => Items.FirstOrDefault(item => item.Id == id);
        public GiftDefinition FindGift(string itemId) => Gifts.FirstOrDefault(gift => gift.ItemId == itemId);
        public NamecardDefinition FindNamecard(string id) => Namecards.FirstOrDefault(card => card.Id == (string.IsNullOrEmpty(id) ? NamecardDefinition.DefaultId : id));
        public CardFaceDefinition FindCardFace(string id) => CardFaces.FirstOrDefault(face => face.Id == (string.IsNullOrEmpty(id) ? CardFaceDefinition.DefaultId : id));

        /// <summary>Changes whenever any definition changes; clients compare it to know their copy is current.</summary>
        public string Revision
        {
            get
            {
                var text = new StringBuilder();
                foreach (var i in Items) text.Append("i|").Append(i.Id).Append('|').Append(i.Name).Append('|').Append(i.Description).Append('|')
                    .Append(i.Icon).Append('|').Append((int)i.Kind).Append('|').Append(i.Price).Append('|').Append(i.Listed).Append('|').Append(i.MaxQuantity).Append('\n');
                foreach (var g in Gifts) text.Append("g|").Append(g.ItemId).Append('|').Append(g.Effect).Append('\n');
                foreach (var n in Namecards) text.Append("n|").Append(n.Id).Append('|').Append(n.Name).Append('|').Append(n.Description).Append('|')
                    .Append(n.Artwork).Append('|').Append(n.Price).Append('|').Append(n.Listed).Append('|').Append(n.Purchasable).Append('|').Append(n.Reward).Append('\n');
                foreach (var c in CardFaces) text.Append("c|").Append(c.Id).Append('|').Append(c.Name).Append('|').Append(c.Description).Append('|')
                    .Append(c.Cards).Append('|').Append(c.Preview).Append('|').Append(c.Price).Append('|').Append(c.Listed).Append('\n');
                using var sha = SHA256.Create();
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Take(12).Select(b => b.ToString("x2")));
            }
        }

        /// <summary>Throws with every problem found; a server must not start with an invalid catalog.</summary>
        public void Validate(IEnumerable<string> requiredItems = null, IEnumerable<string> requiredNamecards = null)
        {
            var errors = new List<string>();
            void Check(bool condition, string message) { if (!condition) errors.Add(message); }

            Check(Items != null && Gifts != null && Namecards != null && CardFaces != null, "Every catalog list is required.");
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Check(Items.Length <= MaxProducts && Namecards.Length <= MaxProducts && CardFaces.Length <= MaxProducts && Gifts.Length <= MaxProducts,
                "Each catalog holds at most " + MaxProducts + " products.");

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in Items)
            {
                if (item == null) { errors.Add("Items.json has an empty entry."); continue; }
                var label = "Item " + item.Id + ": ";
                Check(IsValidId(item.Id), label + "ID must be 1-48 lowercase letters, digits, '_' or '-'.");
                Check(itemIds.Add(item.Id ?? string.Empty), label + "duplicate ID.");
                Check(item.Id == null || !item.Id.StartsWith(NamecardItemId.Prefix, StringComparison.Ordinal) &&
                    !item.Id.StartsWith(CardFaceItemId.Prefix, StringComparison.Ordinal), label + "the namecard- and cardface- prefixes are reserved.");
                Check(!string.IsNullOrWhiteSpace(item.Name), label + "Name is required.");
                Check(!string.IsNullOrWhiteSpace(item.Icon), label + "Icon is required.");
                Check(Enum.IsDefined(typeof(ItemKind), item.Kind), label + "unknown Kind.");
                Check(item.MaxQuantity > 0 && (item.Kind != ItemKind.NonConsumable || item.MaxQuantity == 1),
                    label + "MaxQuantity must be positive, and 1 for permanent items.");
            }

            var giftIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var gift in Gifts)
            {
                if (gift == null) { errors.Add("Gifts.json has an empty entry."); continue; }
                var label = "Gift " + gift.ItemId + ": ";
                Check(giftIds.Add(gift.ItemId ?? string.Empty), label + "listed twice.");
                var item = FindItem(gift.ItemId);
                Check(item != null, label + "must be an item in Items.json.");
                Check(item == null || item.Kind == ItemKind.Consumable, label + "must be a consumable item.");
                Check(!string.IsNullOrWhiteSpace(gift.Effect), label + "Effect address is required.");
            }

            Check(Namecards.Length > 0 && Namecards[0]?.Id == NamecardDefinition.DefaultId, "Namecards.json must start with the default namecard.");
            var namecardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in Namecards)
            {
                if (card == null) { errors.Add("Namecards.json has an empty entry."); continue; }
                var label = "Namecard " + card.Id + ": ";
                Check(IsValidId(card.Id), label + "invalid ID.");
                Check(namecardIds.Add(card.Id ?? string.Empty), label + "duplicate ID.");
                Check(!string.IsNullOrWhiteSpace(card.Name), label + "Name is required.");
                Check(card.IsDefault ? card.Price == 0 && card.Listed && string.IsNullOrEmpty(card.Artwork) : !string.IsNullOrWhiteSpace(card.Artwork),
                    card.IsDefault ? label + "the default namecard is free, listed and local." : label + "Artwork address is required.");
            }

            Check(CardFaces.Length > 0 && CardFaces[0]?.Id == CardFaceDefinition.DefaultId, "CardFaces.json must start with the classic default.");
            var cardFaceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var face in CardFaces)
            {
                if (face == null) { errors.Add("CardFaces.json has an empty entry."); continue; }
                var label = "Card face " + face.Id + ": ";
                Check(IsValidId(face.Id), label + "invalid ID.");
                Check(cardFaceIds.Add(face.Id ?? string.Empty), label + "duplicate ID.");
                Check(!string.IsNullOrWhiteSpace(face.Name), label + "Name is required.");
                Check(!string.IsNullOrWhiteSpace(face.Preview), label + "Preview is required.");
                Check(face.IsDefault ? string.IsNullOrEmpty(face.Cards) : !string.IsNullOrWhiteSpace(face.Cards),
                    face.IsDefault ? label + "the classic deck is local." : label + "Cards address is required.");
                Check(!CardFaceItemId.IsDefaultOwned(face.Id ?? string.Empty) || face.Price == 0 && face.Listed,
                    label + "built-in card faces must stay free and listed.");
            }

            // IDs with dedicated behavior in code must stay configured.
            foreach (var id in requiredItems ?? Enumerable.Empty<string>())
                Check(itemIds.Contains(id), "Items.json must define " + id + ", which the game references.");
            foreach (var id in requiredNamecards ?? Enumerable.Empty<string>())
                Check(namecardIds.Contains(id), "Namecards.json must define " + id + ", which the game references.");

            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }

        public static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 48 &&
            id.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-');
    }
}
