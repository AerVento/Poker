using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    public sealed class FoodChainAchievement : Achievement
    {
        public override string Id => "showdown_food_chain";
        public override string Name => "大鱼吃小鱼，小鱼吃虾米";
        public override string Description => "摊牌时，以同花或更强牌型赢得同一奖池，并击败至少两位牌型类别不同的对手。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/FoodChain-v1";
        public override int SortOrder => 300;
        public override Reward Reward => new Reward(5000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player == null || player.Folded || !player.BestRank.HasValue || player.BestRank.Value < PokerHandRank.Flush) return;
            foreach (var pot in context.Pots.Where(p => p.Chips > 0 && p.Winners.Contains(accountId)))
            {
                var losingRanks = context.Players.Where(other => !other.Folded &&
                    pot.Eligible.Contains(other.AccountId) && !pot.Winners.Contains(other.AccountId) &&
                    player.Beats(other)).Select(other => other.BestRank).Distinct();
                // Two different losing categories give the required strict rank ordering.
                if (losingRanks.Take(2).Count() == 2)
                {
                    progress.Count = Target;
                    return;
                }
            }
        }
    }

    public sealed class PairOfTwosWinsAchievement : Achievement
    {
        public override string Id => "pair_of_twos_wins";
        public override string Name => "这也能行？";
        public override string Description => "摊牌时，仅凭一对 2 赢得任意奖池。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/Cards-v1";
        public override int SortOrder => 301;
        public override Reward Reward => new Reward(5000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player == null || player.Folded || player.BestRank != PokerHandRank.Pair ||
                player.Grades.Count == 0 || player.Grades[0] != (int)PokerRank.Two) return;
            if (context.Pots.Any(p => p.Chips > 0 && p.Winners.Contains(accountId)))
            {
                progress.Count = Target;
            }
        }
    }

    public sealed class RoyalStraightFlushAchievement : Achievement
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
            // Street snapshots are captured after dealing, with the current fold state.
            // Complete immediately; a later fold cannot undo an already earned achievement.
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player != null && !player.Folded && player.BestRank == PokerHandRank.Straight_Flush &&
                player.Grades.Count > 0 && player.Grades[0] == (int)PokerRank.Ace)
            {
                progress.Count = Target;
            }
        }
    }
}
