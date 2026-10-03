using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    // Public fields use Mirror Weaver's generated serializers. Requests never carry account identity, prices to charge or effects.
    public struct InventoryRequest : NetworkMessage
    {
        public ulong RequestId;
    }

    public struct InventoryResponse : NetworkMessage
    {
        public ulong RequestId;
        public ulong Revision;
        public string AccountId;
        public bool Success;
        public InventorySnapshotMessage Snapshot;
    }

    public struct ItemPurchaseRequest : NetworkMessage
    {
        public ulong RequestId;
        public string OperationId;
        public string ItemId;
        public uint Quantity;
        /// <summary>The unit price the player confirmed; a different server price rejects the purchase.</summary>
        public uint QuotedPrice;
        public string CatalogRevision;
        public bool AuthorizeChipConversion;
        public uint AuthorizedShortfall;
    }

    public struct ItemEquipRequest : NetworkMessage
    {
        public ulong RequestId;
        public string OperationId;
        public string ItemId;
    }

    /// <summary>Consume items by one method. Give fills TargetSeatIndex; Use sends one command of quantity 1.</summary>
    public struct ItemConsumeRequest : NetworkMessage
    {
        public ulong RequestId;
        public string OperationId;
        public ItemConsumeMethod Method;
        public string CatalogRevision;
        public bool AuthorizeChipConversion;
        public uint AuthorizedShortfall;
        public ItemConsumeCommandMessage[] Commands;
    }

    public struct ItemConsumeCommandMessage
    {
        public string ItemId;
        public uint Quantity;
        public int TargetSeatIndex;
    }

    public struct ItemActionResponse : NetworkMessage
    {
        public ulong RequestId;
        public string OperationId;
        public string AccountId;
        public ItemAction Action;
        public ItemActionResult Result;
        public bool IsNewlyApplied;
        public uint RequiredConversion;
        /// <summary>Dollars credited by a purchase effect, such as a contract's payout.</summary>
        public uint AppliedRewardDollars;
        public bool HasFailedCondition;
        public PurchaseConditionMessage FailedCondition;
        public bool HasSnapshot;
        public InventorySnapshotMessage Snapshot;
    }

    public struct InventorySnapshotMessage
    {
        public uint Dollars;
        public uint Chips;
        public uint AvailableChips;
        public bool CanExchange;
        /// <summary>Every owned item, including namecards and card faces.</summary>
        public ItemStackMessage[] Items;
        public string EquippedNamecard;
        public string EquippedCardFace;
        public bool CanAct;
        public bool CanEquip;
        public string CatalogRevision;
        public ItemOfferStateMessage[] Offers;
        /// <summary>Owned items the player can use right now.</summary>
        public string[] Usable;
    }

    public struct ItemStackMessage
    {
        public string ItemId;
        public uint Quantity;
    }

    public struct ItemOfferStateMessage
    {
        public string ItemId;
        public bool CanPurchase;
        public ItemActionResult BlockReason;
    }

    public struct PurchaseConditionMessage
    {
        public PurchaseConditionKind Kind;
        public ComparisonType Comparison;
        public uint Threshold;
        public int Version;
    }
}
