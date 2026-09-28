using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when a flush-or-better win beats two different hand categories in one pot.</summary>
    public sealed class FoodChainAchievement : HandAchievement
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
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player == null || player.Folded || !player.BestRank.HasValue || player.BestRank.Value < PokerHandRank.Flush) return;
            foreach (var pot in context.Pots.Where(pot => pot.Chips > 0 && pot.Winners.Contains(accountId)))
            {
                var losingRanks = context.Players.Where(other => !other.Folded && pot.Eligible.Contains(other.AccountId) &&
                    !pot.Winners.Contains(other.AccountId) && player.Beats(other)).Select(other => other.BestRank).Distinct();
                if (losingRanks.Take(2).Count() == 2)
                {
                    progress.Count = Target;
                    return;
                }
            }
        }
    }
}
