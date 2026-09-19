using System;
using System.Collections.Generic;
using Game.Achievements;
using Game.Events;
using Game.Tasks;

namespace Game.Network.Accounts
{
    public enum AchievementResult
    {
        Success, AlreadyClaimed, NotComplete, UnknownAchievement, OutsideRoomRequired,
        BalanceLimit, ItemLimit, InvalidRequest, Unavailable, RateLimited, RevisionChanged
    }

    public sealed class PlayerAchievement
    {
        public string Id { get; set; }
        public AchievementProgress Progress { get; set; } = new AchievementProgress();
        public long CompletedAt { get; set; }
        public long ClaimedAt { get; set; }
        public bool Announced { get; set; }
        public uint RewardChips { get; set; }
        public ItemStack[] RewardItems { get; set; } = Array.Empty<ItemStack>();
        public Dictionary<string, uint> ItemLimits { get; set; } = new Dictionary<string, uint>();
    }

    public sealed class AchievementDocument
    {
        public long Revision { get; set; }
        public List<PlayerAchievement> Achievements { get; set; } = new List<PlayerAchievement>();
    }

    public sealed class PendingAchievementEvent
    {
        public string AccountId { get; set; }
        public PlayerEvent Event { get; set; }
        // Compatibility for callers constructing legacy hand events.
        public AchievementContext Context
        {
            get => (Event as HandStageRecordedEvent)?.Context;
            set => Event = value == null ? null : new HandStageRecordedEvent(value);
        }
    }

    public abstract class AchievementAccountTransaction
    {
        public PlayerAccount Account { get; protected set; }
        public AchievementDocument Document { get; protected set; }
        public bool AlreadyConsumed { get; protected set; }
        public bool Changed { get; set; }
        public abstract bool CanClaim { get; }
        public abstract AchievementResult Grant(Reward reward, IReadOnlyDictionary<string, uint> limits, string claimKey);
    }

    public interface IPlayerEventRepository
    {
        void PublishPlayerEvent(PlayerEvent playerEvent);
    }

    public interface IAchievementRepository
    {
        void InitializeAchievements();
        void PublishAchievementEvent(AchievementContext context);
        PendingAchievementEvent[] ReadPendingAchievementEvents(int limit);
        T TransactAchievements<T>(string accountId, Func<AchievementAccountTransaction, T> operation, string eventId = null);
    }
}
