using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    // Public fields use Mirror Weaver's generated serializers. No account identity or effects in requests.
    public struct ItemShopRequest : NetworkMessage
    {
        public ulong RequestId;
        public ItemShopAction Action;
        public string OperationId;
        public string ItemId;
        public uint Quantity;
        public uint QuotedPrice;
        public string CatalogRevision;
    }

    public struct ItemShopResponse : NetworkMessage
    {
        public ulong RequestId;
        public string OperationId;
        public string AccountId;
        public ItemShopResult Result;
        public bool HasSnapshot;
        public uint Chips;
        public bool CanAct;
        public string CatalogRevision;
        public ItemStackMessage[] Items;
        public ItemOfferMessage[] Offers;
        public bool IsNewlyApplied;
        public bool HasOutcome;
        public uint AppliedRewardChips;
        public bool HasFailedCondition;
        public PurchaseConditionMessage FailedCondition;
        public ItemEffectOutcomeMessage[] Effects;
    }

    public struct ItemStackMessage
    {
        public string ItemId;
        public uint Quantity;
    }

    public struct ItemOfferMessage
    {
        public string ItemId;
        public uint Price;
        public bool Listed;
        public uint RewardChips;
        public uint MaxQuantity;
        public PurchaseConditionMessage[] PurchaseConditions;
        public bool CanPurchase;
        public bool CanUse;
        public uint UseCost;
        public ItemShopResult BlockReason;
    }

    public struct PurchaseConditionMessage
    {
        public PurchaseConditionKind Kind;
        public ComparisonType Comparison;
        public uint Threshold;
        public int Version;
    }

    public struct ItemEffectOutcomeMessage
    {
        public int EffectIndex;
        public int CommandIndex;
        public PurchaseEffectKind Kind;
        public int Version;
        public uint Amount;
        public uint BalanceBefore;
        public uint BalanceAfter;
    }
}
