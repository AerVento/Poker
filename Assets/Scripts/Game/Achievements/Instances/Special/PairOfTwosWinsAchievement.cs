using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when a best hand of exactly one pair of twos wins an actual pot.</summary>
    public sealed class PairOfTwosWinsAchievement : HandAchievement
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
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player == null || player.Folded || player.BestRank != PokerHandRank.Pair || player.Grades.Count == 0 ||
                player.Grades[0] != (int)PokerRank.Two) return;
            if (context.Pots.Any(pot => pot.Chips > 0 && pot.Winners.Contains(accountId))) progress.Count = Target;
        }
    }
}
