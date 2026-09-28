using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes as soon as an active player forms a royal straight flush before folding.</summary>
    public sealed class RoyalStraightFlushAchievement : HandAchievement
    {
        public override string Id => "royal_straight_flush";
        public override string Name => "皇家同花顺";
        public override string Description => "弃牌前组成同花色的 A、K、Q、J、10。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/Champion-v1";
        public override int SortOrder => 302;
        public override Reward Reward => new Reward(50000, new[] { new ItemStack("namecard-royal-straight-flush", 1) });

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player != null && !player.Folded && player.BestRank == PokerHandRank.Straight_Flush &&
                player.Grades.Count > 0 && player.Grades[0] == (int)PokerRank.Ace) progress.Count = Target;
        }
    }
}
