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
        public ItemUsePolicy UsePolicy { get; }

        internal ItemPurchaseProduct(ServerItemDefinition product, ItemPurchasePolicy policy, ItemUsePolicy usePolicy)
        {
            Id = product.Id;
            Kind = product.Kind;
            Price = product.Price;
            Listed = product.Listed;
            MaxQuantity = product.MaxQuantity;
            Policy = policy;
            UsePolicy = usePolicy;
        }
    }

    // Copies authoring data and registration entries. A request observes a single immutable configuration.
    public sealed class ItemPurchaseRuntime
    {
        public IReadOnlyList<ItemPurchaseProduct> Products { get; }
        public string Revision { get; }
        private readonly Dictionary<string, ItemPurchaseProduct> _byId;

        public ItemPurchaseRuntime(ItemServerCatalog catalog, ItemPurchaseRegistry registry, ItemUseRegistry useRegistry = null)
        {
            if (catalog == null || registry == null) throw new ArgumentNullException();
            catalog.Validate();
            var products = catalog.Products.Select(p => new ItemPurchaseProduct(p, registry.Get(p.Id), useRegistry?.Get(p.Id))).ToArray();
            Products = Array.AsReadOnly(products);
            _byId = products.ToDictionary(p => p.Id, StringComparer.Ordinal);
            using var sha = SHA256.Create();
            var versioned = products.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => new
            {
                p.Id, p.Kind, p.Price, p.Listed, p.MaxQuantity,
                Conditions = p.Policy.ConditionDescriptors, Effects = p.Policy.EffectDescriptors,
                UseConditions = p.UsePolicy?.Conditions.Select(c => c.Descriptor).ToArray(),
                UseEffects = p.UsePolicy?.Effects.Select(e => new { Kind = PurchaseEffectKind.DebitChips, e.Version, e.Amount }).ToArray(),
                UseKind = p.UsePolicy?.Kind,
                UseLocations = p.UsePolicy?.AllowedLocations
            });
            Revision = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(versioned)))).Replace("-", "");
        }

        public ItemPurchaseProduct Find(string itemId) => itemId != null && _byId.TryGetValue(itemId, out var product) ? product : null;

        // Used for both read-only eligibility projection and transaction validation. Does not plan effects.
        public static ItemShopResult CheckUse(ItemUseContext context, ItemUseLocation location)
        {
            if (context.Product == null) return ItemShopResult.UnknownItem;
            if (context.OwnedQuantity == 0) return ItemShopResult.NotOwned;
            if (context.Product.Kind != ItemKind.Consumable) return ItemShopResult.NotConsumable;
            var policy = context.Product.UsePolicy;
            if (policy == null) return ItemShopResult.EffectUnavailable;
            if (!policy.Allows(location))
            {
                if ((policy.AllowedLocations & ItemUseLocationMask.StartedRoom) != 0)
                    return ItemShopResult.RoomStartedRequired;
                if ((policy.AllowedLocations & ItemUseLocationMask.OutsideRoom) != 0)
                    return ItemShopResult.OutsideRoomRequired;
                return ItemShopResult.UseLocationNotAllowed;
            }
            if (policy.Conditions.Any(c => !c.Check(context))) return ItemShopResult.UseConditionNotMet;
            return context.BalanceBefore < policy.Cost ? ItemShopResult.InsufficientChips : ItemShopResult.Success;
        }

        // Compatibility for the shop and collection flows. In-room item use must select a precise room phase.
        public static ItemShopResult CheckUse(ItemUseContext context, bool outsideRoom)
            => CheckUse(context, outsideRoom ? ItemUseLocation.OutsideRoom : ItemUseLocation.WaitingRoom);

        public static ItemShopResult CheckPurchase(PurchaseContext context, bool outsideRoom,
            out PurchaseConditionDescriptor failedCondition)
            => CheckPurchase(context, outsideRoom, 1, out failedCondition);

        public static ItemShopResult CheckPurchase(PurchaseContext context, bool outsideRoom, uint quantity,
            out PurchaseConditionDescriptor failedCondition)
        {
            failedCondition = null;
            var product = context.Product;
            if (product == null || !product.Listed) return ItemShopResult.UnknownItem;
            if (quantity == 0 || (quantity > 1 && (product.Kind != ItemKind.Consumable ||
                product.UsePolicy?.Kind != ItemUseKind.Gift || product.Policy.OnPurchaseEffects.Count != 0)))
                return ItemShopResult.InvalidRequest;
            if (product.Kind == ItemKind.NonConsumable && context.OwnedQuantity > 0) return ItemShopResult.AlreadyOwned;
            if ((ulong)context.OwnedQuantity + quantity > product.MaxQuantity) return ItemShopResult.QuantityLimit;
            var totalPrice = (ulong)product.Price * quantity;
            if (totalPrice > context.BalanceBefore) return ItemShopResult.InsufficientChips;
            failedCondition = product.Policy.FirstFailure(context);
            if (failedCondition != null) return ItemShopResult.BalanceConditionNotMet;
            // Known effect descriptions allow read-only range checks; execution checks each actual command again.
            return (ulong)context.BalanceBefore - totalPrice + product.Policy.RewardChips > uint.MaxValue
                ? ItemShopResult.BalanceLimit : ItemShopResult.Success;
        }
    }

    public static class ContractPurchaseRegistration
    {
        private static void RegisterRepayment(ItemUseRegistry registry, string itemId, uint cost)
        {
            registry.Register(itemId, new ItemUsePolicy(
                new IItemUseCondition[] { new UseBalanceCondition(ComparisonType.GreaterThanOrEqual, cost) },
                new IItemUseEffect[] { new PayChipsEffect(cost) },
                ItemUseLocationMask.OutsideRoom | ItemUseLocationMask.WaitingRoom | ItemUseLocationMask.StartedRoom));
        }

        public static ItemPurchaseRuntime CreateRuntime(ItemServerCatalog catalog, IEnumerable<string> gifts)
        {
            var registry = new ItemPurchaseRegistry();
            registry.Register("devil-contract", new ItemPurchasePolicy(
                new IPurchaseCondition[] { new BalanceCondition(ComparisonType.LessThan, 1000) },
                new IPurchaseEffect[] { new RewardChipsEffect(1000) }));
            registry.Register("archdevil-contract", new ItemPurchasePolicy(
                new IPurchaseCondition[] { new BalanceCondition(ComparisonType.LessThan, 10000) },
                new IPurchaseEffect[] { new RewardChipsEffect(10000) }));
            registry.Register("satan-contract", new ItemPurchasePolicy(
                new IPurchaseCondition[] { new BalanceCondition(ComparisonType.LessThan, 100000) },
                new IPurchaseEffect[] { new RewardChipsEffect(100000) }));
            // Gifts come from Configs/Shop/Gifts.json; the server registers their use without loading any effect.
            var giftIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var gift in gifts ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(gift) || !giftIds.Add(gift))
                    throw new InvalidOperationException("Gift catalog requires unique item IDs.");
                registry.Register(gift, new ItemPurchasePolicy());
            }
            catalog.Validate();
            foreach (var product in catalog.Products)
            {
                var contract = product.Id == "devil-contract" || product.Id == "archdevil-contract" || product.Id == "satan-contract";
                if (contract && (product.Kind != ItemKind.Consumable || product.MaxQuantity != uint.MaxValue || product.Price != 0))
                    throw new InvalidOperationException("Contract products must remain free consumables without an inventory limit.");
                if (giftIds.Contains(product.Id) && product.Kind != ItemKind.Consumable)
                    throw new InvalidOperationException("Gift products must be consumable.");
                if (!contract && !giftIds.Contains(product.Id))
                    registry.Register(product.Id, new ItemPurchasePolicy());
            }
            var uses = new ItemUseRegistry();
            RegisterRepayment(uses, "devil-contract", 1100);
            RegisterRepayment(uses, "archdevil-contract", 11000);
            RegisterRepayment(uses, "satan-contract", 110000);
            foreach (var giftId in giftIds)
                uses.Register(giftId, new ItemUsePolicy(
                Array.Empty<IItemUseCondition>(), Array.Empty<IItemUseEffect>(),
                ItemUseLocationMask.StartedRoom, ItemUseKind.Gift));
            return new ItemPurchaseRuntime(catalog, registry, uses);
        }
    }
}
