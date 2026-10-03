namespace Game.Network.Accounts
{
    /// <summary>
    /// Outcome of an item purchase, equip or consume request. Values are persisted in item_requests receipts and sent
    /// on the wire: append new results without renumbering.
    /// </summary>
    public enum ItemActionResult : byte
    {
        Success, InvalidRequest, Unavailable, RateLimited, UnknownItem, CatalogChanged,
        InsufficientDollars, ConversionRequired, QuoteChanged, BalanceLimit,
        AlreadyOwned, QuantityLimit, NotOwned, PurchaseConditionNotMet,
        NotEquippable, OutsideRoomRequired,
        NotConsumable, MethodUnavailable, UseConditionNotMet, LocationNotAllowed, RoomStartedRequired,
        SenderNotSeated, TargetNotFound, CannotTargetSelf, ServiceRestricted, InsufficientItems,
    }

    public sealed class AccountItemInventory
    {
        public PlayerAccount Account { get; set; }
        /// <summary>Every owned item, including namecards and card faces.</summary>
        public ItemStack[] Items { get; set; }
    }

    /// <summary>One consume instruction. Give needs a target seat; Use always consumes one item and has no target.</summary>
    public sealed class ItemConsumeCommand
    {
        public string ItemId { get; }
        public uint Quantity { get; }
        public int TargetSeatIndex { get; }

        public ItemConsumeCommand(string itemId, uint quantity, int targetSeatIndex = -1)
        {
            ItemId = itemId;
            Quantity = quantity;
            TargetSeatIndex = targetSeatIndex;
        }
    }

    public interface IItemRepository
    {
        AccountItemInventory ReadItems(string accountId);

        ItemChangeExecution PurchaseItem(string accountId, string operationId, string itemId, uint quantity,
            uint quotedPrice, string catalogRevision, ItemRules rules, bool authorizeChipConversion = false, uint authorizedShortfall = 0);

        ItemActionResult EquipItem(string accountId, string operationId, string itemId, ItemRules rules);

        /// <summary>
        /// Consumes items by one method in a single transaction. Location checks that need live room state are done by
        /// the caller and passed in as location.
        /// </summary>
        ItemConsumeExecution ConsumeItems(string accountId, string operationId, ItemConsumeMethod method,
            System.Collections.Generic.IReadOnlyList<ItemConsumeCommand> commands, string catalogRevision, ItemRules rules,
            ItemUseLocation location, bool authorizeChipConversion = false, uint authorizedShortfall = 0);
    }
}
