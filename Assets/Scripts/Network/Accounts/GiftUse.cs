using System;
using System.Collections.Generic;

namespace Game.Network.Accounts
{
    public static class GiftItemIds
    {
        public const string RottenEgg = "rotten-egg";
    }

    public static class GiftBatchLimits
    {
        public const int MaxCommands = 32;
        public const uint MaxEffects = 200;
    }

    public enum GiftUseResult : byte
    {
        Success,
        InvalidRequest,
        Unavailable,
        RateLimited,
        RoomNotStarted,
        SenderNotSeated,
        TargetNotFound,
        CannotTargetSelf,
        UnknownGift,
        NotOwned,
        InsufficientItems,
        UseNotAllowed,
        InsufficientDollars,
        ServiceRestricted,
    }

    public sealed class GiftUseCommand
    {
        public string ItemId { get; }
        public int TargetSeatIndex { get; }
        public uint Quantity { get; }

        public GiftUseCommand(string itemId, int targetSeatIndex, uint quantity)
        {
            ItemId = itemId;
            TargetSeatIndex = targetSeatIndex;
            Quantity = quantity;
        }
    }

    public sealed class GiftUseExecution
    {
        public GiftUseResult Result { get; }
        public bool IsNewlyApplied { get; }
        public IReadOnlyList<GiftUseCommand> AppliedCommands { get; }

        public GiftUseExecution(GiftUseResult result, bool isNewlyApplied = false, IReadOnlyList<GiftUseCommand> appliedCommands = null)
        {
            Result = result;
            IsNewlyApplied = isNewlyApplied;
            AppliedCommands = appliedCommands ?? Array.Empty<GiftUseCommand>();
        }
    }

    public interface IGiftRepository
    {
        GiftUseExecution ConsumeGiftBatch(string accountId, string operationId,
            IReadOnlyList<GiftUseCommand> commands, ItemPurchaseRuntime runtime);
    }
}
