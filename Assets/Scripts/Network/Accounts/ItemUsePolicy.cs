using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Network.Accounts
{
    public enum ItemUseLocation : byte
    {
        OutsideRoom,
        WaitingRoom,
        StartedRoom,
    }

    [Flags]
    public enum ItemUseLocationMask : byte
    {
        None = 0,
        OutsideRoom = 1 << 0,
        WaitingRoom = 1 << 1,
        StartedRoom = 1 << 2,
    }

    public enum ItemUseKind : byte
    {
        Standard,
        Gift,
    }

    public sealed class ItemUseContext
    {
        public string AccountId { get; }
        public uint BalanceBefore { get; }
        public ItemPurchaseProduct Product { get; }
        public uint OwnedQuantity { get; }

        public ItemUseContext(string accountId, uint balanceBefore, ItemPurchaseProduct product, uint ownedQuantity)
        {
            AccountId = accountId;
            BalanceBefore = balanceBefore;
            Product = product;
            OwnedQuantity = ownedQuantity;
        }
    }

    // Conditions inspect an immutable snapshot; effects only plan commands for the owning transaction.
    public interface IItemUseCondition
    {
        PurchaseConditionDescriptor Descriptor { get; }
        bool Check(ItemUseContext context);
    }

    public sealed class UseBalanceCondition : IItemUseCondition
    {
        public PurchaseConditionDescriptor Descriptor { get; }
        public UseBalanceCondition(ComparisonType comparison, uint threshold)
            => Descriptor = new PurchaseConditionDescriptor(PurchaseConditionKind.Balance, comparison, threshold);
        public bool Check(ItemUseContext context) => Descriptor.Matches(context.BalanceBefore);
    }

    public sealed class DebitChips
    {
        public uint Amount { get; }
        public DebitChips(uint amount)
        {
            if (amount == 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Amount = amount;
        }
    }

    public interface IItemUseEffect
    {
        uint Amount { get; }
        int Version { get; }
        IReadOnlyList<DebitChips> Plan(ItemUseContext context);
    }

    public sealed class PayChipsEffect : IItemUseEffect
    {
        public uint Amount { get; }
        public int Version => 1;
        public PayChipsEffect(uint amount)
        {
            if (amount == 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Amount = amount;
        }
        public IReadOnlyList<DebitChips> Plan(ItemUseContext context)
            => Array.AsReadOnly(new[] { new DebitChips(Amount) });
    }

    public sealed class ItemUsePolicy
    {
        public IReadOnlyList<IItemUseCondition> Conditions { get; }
        public IReadOnlyList<IItemUseEffect> Effects { get; }
        public uint Cost { get; }
        public ItemUseLocationMask AllowedLocations { get; }
        public ItemUseKind Kind { get; }

        public ItemUsePolicy(IEnumerable<IItemUseCondition> conditions, IEnumerable<IItemUseEffect> effects,
            ItemUseLocationMask allowedLocations = ItemUseLocationMask.OutsideRoom,
            ItemUseKind kind = ItemUseKind.Standard)
        {
            var checks = (conditions ?? Array.Empty<IItemUseCondition>()).ToArray();
            var plans = (effects ?? Array.Empty<IItemUseEffect>()).ToArray();
            if (checks.Length > 16 || plans.Length > 16 || allowedLocations == ItemUseLocationMask.None ||
                !Enum.IsDefined(typeof(ItemUseKind), kind) ||
                checks.Any(c => c?.Descriptor == null) || plans.Any(e => e == null || e.Amount == 0 || e.Version != 1) ||
                (kind == ItemUseKind.Standard && plans.Length == 0) ||
                (kind == ItemUseKind.Gift && plans.Length != 0))
                throw new ArgumentException("Invalid item use policy.");
            Conditions = Array.AsReadOnly(checks);
            Effects = Array.AsReadOnly(plans);
            AllowedLocations = allowedLocations;
            Kind = kind;
            foreach (var effect in plans) Cost = checked(Cost + effect.Amount);
        }

        public bool Allows(ItemUseLocation location)
        {
            var flag = location switch
            {
                ItemUseLocation.OutsideRoom => ItemUseLocationMask.OutsideRoom,
                ItemUseLocation.WaitingRoom => ItemUseLocationMask.WaitingRoom,
                ItemUseLocation.StartedRoom => ItemUseLocationMask.StartedRoom,
                _ => ItemUseLocationMask.None,
            };
            return (AllowedLocations & flag) != 0;
        }
    }

    public sealed class ItemUseRegistry
    {
        private readonly Dictionary<string, ItemUsePolicy> _policies = new Dictionary<string, ItemUsePolicy>(StringComparer.Ordinal);
        public void Register(string itemId, ItemUsePolicy policy)
        {
            if (!NamecardServerCatalog.IsValidId(itemId) || policy == null || _policies.ContainsKey(itemId))
                throw new ArgumentException("Item use registrations require a unique valid ID and policy.");
            _policies.Add(itemId, policy);
        }
        internal ItemUsePolicy Get(string itemId) => _policies.TryGetValue(itemId, out var policy) ? policy : null;
    }
}
