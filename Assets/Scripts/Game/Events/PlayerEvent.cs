using System;
using System.Collections.Generic;
using System.Linq;
using Game.Achievements;
using Game.Network;

namespace Game.Events
{
    // Immutable, server-only facts. These types must never enter the client protocol.
    public abstract class PlayerEvent
    {
        public abstract string EventId { get; }
        public abstract string SourceId { get; }
        public abstract long OccurredAt { get; }
        public abstract IReadOnlyList<string> AccountIds { get; }
    }

    public abstract class AccountEvent : PlayerEvent
    {
        public override string EventId { get; }
        public string AccountId { get; }
        public string OperationId { get; }
        public override long OccurredAt { get; }
        public override IReadOnlyList<string> AccountIds { get; }

        protected AccountEvent(string eventId, string accountId, string operationId, long occurredAt)
        {
            EventId = eventId;
            AccountId = accountId;
            OperationId = operationId;
            OccurredAt = occurredAt;
            AccountIds = Array.AsReadOnly(new[] { accountId });
        }
    }

    public sealed class HandStageRecordedEvent : PlayerEvent
    {
        public AchievementContext Context { get; }
        public override string EventId => Context.EventId;
        public override string SourceId => TableEventSource.Id;
        public override long OccurredAt => Context.OccurredAt;
        public override IReadOnlyList<string> AccountIds { get; }

        public HandStageRecordedEvent(AchievementContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            AccountIds = Array.AsReadOnly(context.Players.Select(p => p.AccountId).ToArray());
        }
    }

    public sealed class HandActionRecordedEvent : AccountEvent
    {
        public override string SourceId => TableEventSource.Id;
        public string HandId { get; }
        public IReadOnlyList<Poker> Hole { get; }
        public AchievementAction Action { get; }

        public HandActionRecordedEvent(string eventId, string accountId, string operationId, long occurredAt,
            string handId, IReadOnlyList<Poker> hole, AchievementAction action)
            : base(eventId, accountId, operationId, occurredAt)
        {
            HandId = handId;
            Hole = Array.AsReadOnly(hole.ToArray());
            Action = action;
        }
    }

    public sealed class HandCardsRevealedEvent : AccountEvent
    {
        public override string SourceId => TableEventSource.Id;
        public string HandId { get; }
        public IReadOnlyList<Poker> Community { get; }
        public IReadOnlyList<Poker> Hole { get; }

        public HandCardsRevealedEvent(string eventId, string accountId, string operationId, long occurredAt,
            string handId, IReadOnlyList<Poker> community, IReadOnlyList<Poker> hole)
            : base(eventId, accountId, operationId, occurredAt)
        {
            HandId = handId;
            Community = Array.AsReadOnly((community ?? Array.Empty<Poker>()).ToArray());
            Hole = Array.AsReadOnly((hole ?? Array.Empty<Poker>()).ToArray());
        }
    }

    public sealed class ItemPurchasedEvent : AccountEvent
    {
        public override string SourceId => ShopEventSource.Id;
        public string ProductKind { get; }
        public string ItemId { get; }
        public uint Quantity { get; }
        public uint PaidChips { get; }

        public ItemPurchasedEvent(string eventId, string accountId, string operationId, long occurredAt,
            string productKind, string itemId, uint quantity, uint paidChips)
            : base(eventId, accountId, operationId, occurredAt)
        {
            ProductKind = productKind;
            ItemId = itemId;
            Quantity = quantity;
            PaidChips = paidChips;
        }
    }

    public sealed class ItemUsedEvent : AccountEvent
    {
        public override string SourceId => InventoryEventSource.Id;
        public string ItemId { get; }
        public uint Quantity { get; }
        public uint PaidChips { get; }
        // -1 for untargeted use. A gift records accepted consumption, not animation hits.
        public int TargetSeat { get; }

        public ItemUsedEvent(string eventId, string accountId, string operationId, long occurredAt,
            string itemId, uint quantity, uint paidChips, int targetSeat)
            : base(eventId, accountId, operationId, occurredAt)
        {
            ItemId = itemId;
            Quantity = quantity;
            PaidChips = paidChips;
            TargetSeat = targetSeat;
        }
    }

    public sealed class InventoryChangedEvent : AccountEvent
    {
        public override string SourceId => InventoryEventSource.Id;
        public string Reason { get; }
        public IReadOnlyList<ItemStack> Items { get; }

        public InventoryChangedEvent(string eventId, string accountId, string operationId, long occurredAt,
            string reason, IReadOnlyList<ItemStack> items)
            : base(eventId, accountId, operationId, occurredAt)
        {
            Reason = reason;
            Items = Array.AsReadOnly(items.ToArray());
        }
    }

    public sealed class EquipmentChangedEvent : AccountEvent
    {
        public override string SourceId => CollectionEventSource.Id;
        public string Slot { get; }
        public string PreviousItemId { get; }
        public string ItemId { get; }
        public string EquippedNamecardId { get; }
        public string EquippedCardFaceId { get; }

        public EquipmentChangedEvent(string eventId, string accountId, string operationId, long occurredAt,
            string slot, string previousItemId, string itemId, string equippedNamecardId, string equippedCardFaceId)
            : base(eventId, accountId, operationId, occurredAt)
        {
            Slot = slot;
            PreviousItemId = previousItemId;
            ItemId = itemId;
            EquippedNamecardId = equippedNamecardId;
            EquippedCardFaceId = equippedCardFaceId;
        }
    }
}
