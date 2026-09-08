using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Network.Accounts
{
    public sealed class ItemPurchaseProduct
    {
        public string Id { get; }
        public ItemKind Kind { get; }
        public uint Price { get; }
        public bool Listed { get; }
        public uint MaxQuantity { get; }
        public ItemPurchasePolicy Policy { get; }

        internal ItemPurchaseProduct(ServerItemDefinition product, ItemPurchasePolicy policy)
        {
            Id = product.Id;
            Kind = product.Kind;
            Price = product.Price;
            Listed = product.Listed;
            MaxQuantity = product.MaxQuantity;
            Policy = policy;
        }
    }

    // Copies authoring data and registration entries. A request observes a single immutable configuration.
    public sealed class ItemPurchaseRuntime
    {
        public IReadOnlyList<ItemPurchaseProduct> Products { get; }
        public string Revision { get; }
        private readonly Dictionary<string, ItemPurchaseProduct> _byId;

        public ItemPurchaseRuntime(ItemServerCatalog catalog, ItemPurchaseRegistry registry)
        {
            if (catalog == null || registry == null) throw new ArgumentNullException();
            catalog.Validate();
            var products = catalog.Products.Select(p => new ItemPurchaseProduct(p, registry.Get(p.Id))).ToArray();
            Products = Array.AsReadOnly(products);
            _byId = products.ToDictionary(p => p.Id, StringComparer.Ordinal);
            using var sha = SHA256.Create();
            var versioned = products.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => new
            {
                p.Id, p.Kind, p.Price, p.Listed, p.MaxQuantity,
                Conditions = p.Policy.ConditionDescriptors, Effects = p.Policy.EffectDescriptors
            });
            Revision = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(versioned)))).Replace("-", "");
        }

        public ItemPurchaseProduct Find(string itemId) => itemId != null && _byId.TryGetValue(itemId, out var product) ? product : null;

        // Used for both read-only eligibility projection and transaction validation. Does not plan effects.
        public static ItemShopResult CheckPurchase(PurchaseContext context, bool outsideRoom,
            out PurchaseConditionDescriptor failedCondition)
        {
            failedCondition = null;
            if (!outsideRoom) return ItemShopResult.OutsideRoomRequired;
            var product = context.Product;
            if (product == null || !product.Listed) return ItemShopResult.UnknownItem;
            if (product.Kind == ItemKind.NonConsumable && context.OwnedQuantity > 0) return ItemShopResult.AlreadyOwned;
            if (context.OwnedQuantity >= product.MaxQuantity) return ItemShopResult.QuantityLimit;
            if (context.BalanceBefore < product.Price) return ItemShopResult.InsufficientChips;
            failedCondition = product.Policy.FirstFailure(context);
            if (failedCondition != null) return ItemShopResult.BalanceConditionNotMet;
            // Known effect descriptions allow read-only range checks; execution checks each actual command again.
            return (ulong)(context.BalanceBefore - product.Price) + product.Policy.RewardChips > uint.MaxValue
                ? ItemShopResult.BalanceLimit : ItemShopResult.Success;
        }
    }

    public static class ContractPurchaseRegistration
    {
        public static ItemPurchaseRuntime CreateRuntime(ItemServerCatalog catalog)
        {
            var registry = new ItemPurchaseRegistry();
            registry.Register("devil-contract", new ItemPurchasePolicy(
                new IPurchaseCondition[] { new BalanceCondition(ComparisonType.Equal, 0) },
                new IPurchaseEffect[] { new RewardChipsEffect(1000) }));
            registry.Register("archdevil-contract", new ItemPurchasePolicy(
                new IPurchaseCondition[] { new BalanceCondition(ComparisonType.Equal, 0) },
                new IPurchaseEffect[] { new RewardChipsEffect(10000) }));
            registry.Register("satan-contract", new ItemPurchasePolicy(
                new IPurchaseCondition[] { new BalanceCondition(ComparisonType.Equal, 0) },
                new IPurchaseEffect[] { new RewardChipsEffect(100000) }));
            catalog.Validate();
            foreach (var product in catalog.Products)
                if (product.Price != 0 || product.Kind != ItemKind.NonConsumable || product.MaxQuantity != 1)
                    throw new InvalidOperationException("Contract catalog must use free permanent products with limit one.");
            return new ItemPurchaseRuntime(catalog, registry);
        }
    }
}
