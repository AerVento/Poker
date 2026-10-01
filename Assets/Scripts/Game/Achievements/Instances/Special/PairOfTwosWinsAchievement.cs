using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when exactly one pair of twos wins a pot against only high-card opponents.</summary>
    public sealed class PairOfTwosWinsAchievement : HandAchievement
    {
        public override string Id => "pair_of_twos_wins";
        public override string Name => "这也能行？";
        public override string Description => "摊牌时，以一对 2 赢得奖池，且同池败者均为高牌。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/cards";
        public override int SortOrder => 301;
        public override Reward Reward => new Reward(5000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player == null || player.Folded || player.BestRank != PokerHandRank.Pair || player.Grades.Count == 0 ||
                player.Grades[0] != (int)PokerRank.Two) return;
            foreach (var pot in context.Pots.Where(pot => pot.Chips > 0 && pot.Eligible.Contains(accountId) &&
                pot.Winners.Contains(accountId)))
            {
                var losers = context.Players.Where(opponent => !opponent.Folded &&
                    pot.Eligible.Contains(opponent.AccountId) && !pot.Winners.Contains(opponent.AccountId)).ToArray();
                if (losers.Length > 0 && losers.All(opponent => opponent.BestRank == PokerHandRank.HighCard))
                {
                    progress.Count = Target;
                    return;
                }
            }
        }
    }
}
