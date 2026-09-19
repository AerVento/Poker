using System;
using System.Collections.Generic;
using System.Text.Json;
using Game.Achievements;

namespace Game.Events
{
    public sealed class TableEventSource : IPlayerEventSource
    {
        public const string Id = "table";
        public string SourceId => Id;
        public void Register(PlayerEventRegistry registry)
        {
            // Keep the original JSON representation and event IDs so schema-10/11 events replay unchanged.
            registry.Register<HandStageRecordedEvent>(Id, "stage", 1,
                value => JsonSerializer.Serialize(value.Context),
                json => new HandStageRecordedEvent(JsonSerializer.Deserialize<AchievementContext>(json)));
            registry.Register<HandActionRecordedEvent>(Id, "action", 1);
            registry.Register<HandActionRecordedEvent>(Id, "action", 2);
            registry.Register<HandCardsRevealedEvent>(Id, "revealed", 1);
        }

        public static HandActionRecordedEvent Action(string handId, IReadOnlyList<Poker> hole,
            AchievementAction action, long occurredAt)
        {
            return new HandActionRecordedEvent(handId + ":action:" + action.TurnId,
                action.AccountId, handId, occurredAt, handId, hole, action);
        }

        public static HandCardsRevealedEvent Revealed(string handId, string accountId,
            IReadOnlyList<Poker> community, IReadOnlyList<Poker> hole, long occurredAt)
        {
            return new HandCardsRevealedEvent(handId + ":reveal:" + accountId,
                accountId, handId, occurredAt, handId, community, hole);
        }
    }

    public sealed class ShopEventSource : IPlayerEventSource
    {
        public const string Id = "shop";
        public string SourceId => Id;
        public void Register(PlayerEventRegistry registry) => registry.Register<ItemPurchasedEvent>(Id, "purchased", 1);

        public static void Purchased(IPlayerEventWriter writer, string accountId, string operationId,
            long occurredAt, string kind, string itemId, uint quantity, uint paidChips)
        {
            writer.Append(new ItemPurchasedEvent($"shop:{kind}:{accountId}:{operationId}:purchased",
                accountId, operationId, occurredAt, kind, itemId, quantity, paidChips));
        }
    }

    public sealed class InventoryEventSource : IPlayerEventSource
    {
        public const string Id = "inventory";
        public string SourceId => Id;
        public void Register(PlayerEventRegistry registry)
        {
            registry.Register<ItemUsedEvent>(Id, "used", 1);
            registry.Register<InventoryChangedEvent>(Id, "changed", 1);
        }

        public static void Used(IPlayerEventWriter writer, string accountId, string operationId,
            long occurredAt, string itemId, uint quantity, uint paidChips, int targetSeat = -1, int index = 0)
        {
            writer.Append(new ItemUsedEvent($"inventory:{accountId}:{operationId}:used:{index}",
                accountId, operationId, occurredAt, itemId, quantity, paidChips, targetSeat));
        }

        public static void Changed(IPlayerEventWriter writer, string accountId, string operationId,
            long occurredAt, string reason, IReadOnlyList<ItemStack> items)
        {
            writer.Append(new InventoryChangedEvent($"inventory:{reason}:{accountId}:{operationId}:changed",
                accountId, operationId, occurredAt, reason, items));
        }
    }

    public sealed class CollectionEventSource : IPlayerEventSource
    {
        public const string Id = "collection";
        public string SourceId => Id;
        public void Register(PlayerEventRegistry registry) => registry.Register<EquipmentChangedEvent>(Id, "equipped", 1);
        public static string Normalize(string value) => string.IsNullOrEmpty(value) ? "default" : value;

        public static void Equipped(IPlayerEventWriter writer, string accountId, string operationId,
            long occurredAt, string slot, string previous, string next, string namecard, string cardFace)
        {
            previous = Normalize(previous);
            next = Normalize(next);
            if (previous == next) return;
            writer.Append(new EquipmentChangedEvent($"collection:{slot}:{accountId}:{operationId}:equipped",
                accountId, operationId, occurredAt, slot, previous, next, Normalize(namecard), Normalize(cardFace)));
        }
    }
}
