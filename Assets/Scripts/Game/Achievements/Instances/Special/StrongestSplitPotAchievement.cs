using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes for a two-way split pot where both winners hold at least a full house.</summary>
    public sealed class StrongestSplitPotAchievement : HandAchievement
    {
        public override string Id => "strongest_split_pot";
        public override string Name => "史上最强vs现代最强";
        public override string Description => "摊牌时，双方以葫芦或更强牌型平分奖池。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/champion";
        public override int SortOrder => 306;
        public override Reward Reward => new Reward(5000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player == null || player.Folded || !player.BestRank.HasValue || player.BestRank.Value < PokerHandRank.Full_House) return;
            foreach (var pot in context.Pots.Where(pot => pot.Chips > 0 && pot.Winners.Count == 2 && pot.Winners.Contains(accountId)))
            {
                var opponent = context.Players.FirstOrDefault(other => other.AccountId != accountId && pot.Winners.Contains(other.AccountId));
                if (opponent != null && !opponent.Folded && opponent.BestRank.HasValue && opponent.BestRank.Value >= PokerHandRank.Full_House &&
                    !player.Beats(opponent) && !opponent.Beats(player))
                {
                    progress.Count = Target;
                    return;
                }
            }
        }
    }
}
