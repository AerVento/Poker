using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Game.Network.Accounts
{
    public sealed class ItemEffectOutcome
    {
        public int EffectIndex { get; }
        public int CommandIndex { get; }
        public PurchaseEffectKind Kind { get; }
        public int Version { get; }
        public uint Amount { get; }
        public uint BalanceBefore { get; }
        public uint BalanceAfter { get; }

        [JsonConstructor]
        public ItemEffectOutcome(int effectIndex, int commandIndex, PurchaseEffectKind kind, int version,
            uint amount, uint balanceBefore, uint balanceAfter)
        {
            EffectIndex = effectIndex;
            CommandIndex = commandIndex;
            Kind = kind;
            Version = version;
            Amount = amount;
            BalanceBefore = balanceBefore;
            BalanceAfter = balanceAfter;
        }
    }

    public sealed class ItemPurchaseOutcome
    {
        public int FormatVersion { get; }
        [JsonIgnore] public string Currency => FormatVersion >= 2 ? "USD" : "LegacyBalance";
        public string ItemId { get; }
        public uint ChargedPrice { get; }
        public uint BalanceBefore { get; }
        public uint BalanceAfter { get; }
        public string RuleRevision { get; }
        public PurchaseConditionDescriptor FailedCondition { get; }
        public IReadOnlyList<ItemEffectOutcome> Effects { get; }
        [JsonIgnore] public uint RewardChips => checked((uint)Effects.Where(e => e.Kind == PurchaseEffectKind.CreditChips).Sum(e => (long)e.Amount));

        [JsonConstructor]
        public ItemPurchaseOutcome(int formatVersion, string itemId, uint chargedPrice, uint balanceBefore,
            uint balanceAfter, string ruleRevision, PurchaseConditionDescriptor failedCondition, IReadOnlyList<ItemEffectOutcome> effects)
        {
            if (formatVersion != 1 && formatVersion != 2) throw new InvalidOperationException("Unsupported item receipt version.");
            FormatVersion = formatVersion;
            ItemId = itemId;
            ChargedPrice = chargedPrice;
            BalanceBefore = balanceBefore;
            BalanceAfter = balanceAfter;
            RuleRevision = ruleRevision;
            FailedCondition = failedCondition;
            Effects = Array.AsReadOnly((effects ?? Array.Empty<ItemEffectOutcome>()).ToArray());
        }
    }

    public sealed class ItemChangeExecution
    {
        public ItemActionResult Result { get; }
        public ItemPurchaseOutcome Outcome { get; }
        public bool IsNewlyApplied { get; }

        public ItemChangeExecution(ItemActionResult result, ItemPurchaseOutcome outcome = null, bool isNewlyApplied = false)
        {
            Result = result;
            Outcome = outcome;
            IsNewlyApplied = isNewlyApplied;
        }
    }

    /// <summary>Applied consume commands, in request order; a give batch may accept only a prefix.</summary>
    public sealed class ItemConsumeExecution
    {
        public ItemActionResult Result { get; }
        public bool IsNewlyApplied { get; }
        public IReadOnlyList<ItemConsumeCommand> Applied { get; }

        public ItemConsumeExecution(ItemActionResult result, bool isNewlyApplied = false, IEnumerable<ItemConsumeCommand> applied = null)
        {
            Result = result;
            IsNewlyApplied = isNewlyApplied;
            Applied = Array.AsReadOnly((applied ?? Array.Empty<ItemConsumeCommand>()).ToArray());
        }
    }
}
