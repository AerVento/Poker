using System;
using System.Linq;
using Game.Network;

namespace Game.Achievements
{
    /// <summary>
    /// Provides the shared settlement calculation for profit-based achievements.
    /// </summary>
    public abstract class NetWinningsAchievement : HandAchievement
    {
        public override string Category => AchievementCategories.Wealth;
        protected abstract bool Cumulative { get; }
        public override string IconPath => Cumulative ? "UI/Achievements/Wealth-v1" : "UI/Achievements/Champion-v1";

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver) return;
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player == null) return;

            // A refund is returned stake, so it offsets the hand contribution rather than counting twice.
            var profit = (long)player.Award + player.Refund - player.Contribution;
            if (profit <= 0) return;
            var count = Cumulative ? (long)progress.Count + profit : Math.Max(progress.Count, profit);
            progress.Count = (int)Math.Min(Target, count);
        }
    }
}
