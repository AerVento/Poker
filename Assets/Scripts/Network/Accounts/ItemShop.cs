using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Network.Accounts
{
    public enum ItemShopAction : byte { Read, Purchase, Use }
    public enum ItemShopResult : byte
    {
        Success, InsufficientChips, NotOwned, UnknownItem, CatalogChanged,
        OutsideRoomRequired, Unavailable, InvalidRequest, RateLimited,
        AlreadyOwned, QuantityLimit, NotConsumable, EffectUnavailable, BalanceConditionNotMet, BalanceLimit
    }

    public sealed class ServerItemDefinition
    {
        public string Id { get; set; }
        public ItemKind Kind { get; set; } = ItemKind.NonConsumable;
        public uint Price { get; set; }
        public bool Listed { get; set; } = true;
        public uint MaxQuantity { get; set; } = 1;
    }

    public sealed class ItemServerCatalog
    {
        public ServerItemDefinition[] Products { get; set; } = Array.Empty<ServerItemDefinition>();

        public string Revision
        {
            get
            {
                using var sha = SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(Products.OrderBy(p => p.Id, StringComparer.Ordinal)))))
                    .Replace("-", "");
            }
        }

        public void Validate()
        {
            if (Products == null || Products.Length > 500)
                throw new InvalidOperationException("Item catalog must contain at most 500 products.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var product in Products)
            {
                if (product == null || !NamecardServerCatalog.IsValidId(product.Id) || !ids.Add(product.Id) ||
                    !Enum.IsDefined(typeof(ItemKind), product.Kind) || product.MaxQuantity == 0 ||
                    (product.Kind == ItemKind.NonConsumable && product.MaxQuantity != 1))
                    throw new InvalidOperationException("Invalid item ID, kind or quantity limit.");
            }
        }
    }

    public sealed class AccountItemInventory
    {
        public PlayerAccount Account { get; set; }
        public ItemStack[] Items { get; set; }
        public bool CanAct { get; set; }
    }

    public interface IItemRepository
    {
        AccountItemInventory ReadItems(string accountId);
        ItemChangeExecution ChangeItem(string accountId, string operationId, ItemShopAction action,
            string itemId, uint quantity, uint quotedPrice, string catalogRevision, ItemPurchaseRuntime runtime, bool inRoom = false);
    }
}
