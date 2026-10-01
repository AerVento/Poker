using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Network.Accounts
{
    public sealed class PurchaseContext
    {
        public string AccountId { get; }
        public uint BalanceBefore { get; }
        public ulong TotalAssets { get; }
        public ItemPurchaseProduct Product { get; }
        public uint OwnedQuantity { get; }

        public PurchaseContext(string accountId, uint balanceBefore, ItemPurchaseProduct product, uint ownedQuantity, ulong? totalAssets = null)
        {
            AccountId = accountId;
            BalanceBefore = balanceBefore;
            TotalAssets = totalAssets ?? balanceBefore;
            Product = product;
            OwnedQuantity = ownedQuantity;
        }
    }

    // Implementations must be immutable and side-effect free. Conditions never plan effects.
    public interface IPurchaseCondition
    {
        PurchaseConditionDescriptor Descriptor { get; }
        bool Check(PurchaseContext context);
    }

    public sealed class BalanceCondition : IPurchaseCondition
    {
        public PurchaseConditionDescriptor Descriptor { get; }

        public BalanceCondition(ComparisonType comparison, uint threshold)
        {
            Descriptor = new PurchaseConditionDescriptor(PurchaseConditionKind.Balance, comparison, threshold);
        }

        public bool Check(PurchaseContext context) => Descriptor.Matches(context.TotalAssets);
    }

    public enum PurchaseEffectKind : byte { CreditChips = 1, DebitChips = 2 }

    public sealed class PurchaseEffectDescriptor
    {
        public PurchaseEffectKind Kind { get; }
        public int Version { get; }
        public uint Amount { get; }

        public PurchaseEffectDescriptor(PurchaseEffectKind kind, uint amount, int version = 1)
        {
            if (kind != PurchaseEffectKind.CreditChips || amount == 0 || version != 1)
                throw new ArgumentException("Unsupported purchase effect.");
            Kind = kind;
            Amount = amount;
            Version = version;
        }
    }

    public abstract class PurchaseEffectCommand { }

    public sealed class CreditChips : PurchaseEffectCommand
    {
        public uint Amount { get; }

        public CreditChips(uint amount)
        {
            if (amount == 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Amount = amount;
        }
    }

    // Planning cannot touch the database. All commands are executed by the owning purchase transaction.
    public interface IPurchaseEffect
    {
        PurchaseEffectDescriptor Descriptor { get; }
        IReadOnlyList<PurchaseEffectCommand> Plan(PurchaseContext context);
    }

    public sealed class RewardChipsEffect : IPurchaseEffect
    {
        public PurchaseEffectDescriptor Descriptor { get; }

        public RewardChipsEffect(uint amount)
        {
            Descriptor = new PurchaseEffectDescriptor(PurchaseEffectKind.CreditChips, amount);
        }

        public IReadOnlyList<PurchaseEffectCommand> Plan(PurchaseContext context)
            => Array.AsReadOnly<PurchaseEffectCommand>(new PurchaseEffectCommand[] { new CreditChips(Descriptor.Amount) });
    }

    public sealed class ItemPurchasePolicy
    {
        public IReadOnlyList<IPurchaseCondition> Conditions { get; }
        public IReadOnlyList<IPurchaseEffect> OnPurchaseEffects { get; }
        public IReadOnlyList<PurchaseConditionDescriptor> ConditionDescriptors { get; }
        public IReadOnlyList<PurchaseEffectDescriptor> EffectDescriptors { get; }
        public uint RewardChips { get; }

        public ItemPurchasePolicy(IEnumerable<IPurchaseCondition> conditions = null, IEnumerable<IPurchaseEffect> onPurchaseEffects = null)
        {
            var checks = (conditions ?? Array.Empty<IPurchaseCondition>()).ToArray();
            var effects = (onPurchaseEffects ?? Array.Empty<IPurchaseEffect>()).ToArray();
            if (checks.Length > 16 || effects.Length > 16 || checks.Any(c => c?.Descriptor == null) || effects.Any(e => e?.Descriptor == null))
                throw new ArgumentException("Invalid purchase policy.");
            Conditions = Array.AsReadOnly(checks);
            OnPurchaseEffects = Array.AsReadOnly(effects);
            ConditionDescriptors = Array.AsReadOnly(checks.Select(c => c.Descriptor).ToArray());
            EffectDescriptors = Array.AsReadOnly(effects.Select(e => e.Descriptor).ToArray());
            foreach (var effect in EffectDescriptors) RewardChips = checked(RewardChips + effect.Amount);
        }

        public PurchaseConditionDescriptor FirstFailure(PurchaseContext context)
        {
            for (int i = 0; i < Conditions.Count; i++)
                if (!Conditions[i].Check(context)) return ConditionDescriptors[i];
            return null;
        }
    }

    public sealed class ItemPurchaseRegistry
    {
        private readonly Dictionary<string, ItemPurchasePolicy> _policies = new Dictionary<string, ItemPurchasePolicy>(StringComparer.Ordinal);

        public void Register(string itemId, ItemPurchasePolicy policy)
        {
            if (!NamecardServerCatalog.IsValidId(itemId) || policy == null || _policies.ContainsKey(itemId))
                throw new ArgumentException("Purchase registrations require a unique valid ID and policy.");
            _policies.Add(itemId, policy);
        }

        internal ItemPurchasePolicy Get(string itemId)
        {
            if (!_policies.TryGetValue(itemId, out var policy))
                throw new InvalidOperationException("Missing purchase policy for " + itemId);
            return policy;
        }
    }
}
