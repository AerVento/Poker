using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Game.Network.Accounts
{
    public enum ShopAction : byte { Read, Purchase, Equip }
    public enum ShopResult : byte
    {
        Success, AlreadyOwned, InsufficientChips, NotOwned, UnknownProduct,
        CatalogChanged, OutsideRoomRequired, Unavailable, InvalidRequest, RateLimited, ConversionRequired, QuoteChanged, BalanceLimit
    }

    [Serializable]
    public sealed class NamecardOffer
    {
        public string Id { get; set; }
        public uint Price { get; set; }
        public bool Listed { get; set; } = true;
    }

    public sealed class NamecardServerCatalog
    {
        public NamecardOffer[] Products { get; set; } = Array.Empty<NamecardOffer>();

        public string Revision
        {
            get
            {
                using var sha = SHA256.Create();
                var value = string.Join("\n", Products.Select(p => p.Id + ":" +
                    p.Price.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + p.Listed));
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
            }
        }

        public void Validate()
        {
            if (Products == null || Products.Length == 0 || Products.Length > 500 ||
                Products[0] == null || Products[0].Id != "default" || Products[0].Price != 0 || !Products[0].Listed)
                throw new InvalidOperationException("The catalog must start with the free default namecard.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var product in Products)
            {
                if (product == null || !IsValidId(product.Id) || !ids.Add(product.Id))
                    throw new InvalidOperationException("Namecard IDs must be valid and unique.");
            }
        }

        public static bool IsValidId(string id)
        {
            return !string.IsNullOrEmpty(id) && id.Length <= 48 &&
                id.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-');
        }
    }

    public sealed class NamecardInventory
    {
        public PlayerAccount Account { get; set; }
        public string[] OwnedIds { get; set; }
        public bool CanAct { get; set; }
    }

    public interface INamecardRepository
    {
        NamecardInventory ReadNamecards(string accountId);
        ShopResult ChangeNamecard(string accountId, string operationId, ShopAction action,
            string productId, string catalogRevision, NamecardServerCatalog catalog);
    }
}
