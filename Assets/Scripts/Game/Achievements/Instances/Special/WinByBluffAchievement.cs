using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when a post-flop fold win outclasses a player who folded in that final betting street.</summary>
    public sealed class WinByBluffAchievement : HandAchievement
    {
        public override string Id => "win_by_bluff";
        public override string Name => "兵不厌诈";
        public override string Description => "翻牌后，靠对手弃牌赢池，且弃牌者中有人牌型高出自己至少两级。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/Bluff-v1";
        public override int SortOrder => 307;
        public override Reward Reward => new Reward(10000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || context.Showdown) return;
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player == null || player.Folded || player.Hole.Count != 2 ||
                !context.Pots.Any(pot => pot.Chips > 0 && pot.Winners.Contains(accountId))) return;
            var community = context.Streets[context.Streets.Count - 1].Community;
            if (community.Count < 3 || community.Count > 5 || !context.PreviousStage.HasValue) return;

            var playerRank = AchievementPatternEvaluator.GetBest(community.Concat(player.Hole).ToArray()).Rank;
            var foldedThisStreet = context.Actions.Where(action => action.Stage == context.PreviousStage.Value &&
                    action.Type == PlayerOperationType.Fold).Select(action => action.AccountId).ToHashSet();
            foreach (var opponent in context.Players.Where(opponent => opponent.AccountId != accountId && opponent.Folded &&
                opponent.Hole.Count == 2 && foldedThisStreet.Contains(opponent.AccountId)))
            {
                // Folded hands are server-only; compare them against the board actually dealt when the hand ended.
                var opponentRank = AchievementPatternEvaluator.GetBest(community.Concat(opponent.Hole).ToArray()).Rank;
                if ((int)opponentRank - (int)playerRank >= 2)
                {
                    progress.Count = Target;
                    return;
                }
            }
        }
    }
}
