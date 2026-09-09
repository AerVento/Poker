using System;
using Game.Achievements;

namespace Game.Network.Accounts
{
    public sealed partial class GuardedAccountRepository : IAchievementRepository
    {
        private IAchievementRepository Achievements => _inner as IAchievementRepository
            ?? throw new InvalidOperationException("The account repository does not support achievements.");
        public void InitializeAchievements() => Execute(() => { Achievements.InitializeAchievements(); return true; });
        public void PublishAchievementEvent(AchievementContext context)
        {
            if (_inner is IAchievementRepository repository)
                Execute(() => { repository.PublishAchievementEvent(context); return true; });
        }
        public PendingAchievementEvent[] ReadPendingAchievementEvents(int limit) => Execute(() => Achievements.ReadPendingAchievementEvents(limit));
        public T TransactAchievements<T>(string accountId, Func<AchievementAccountTransaction, T> operation, string eventId = null)
            => Execute(() => Achievements.TransactAchievements(accountId, operation, eventId));
    }
}
