using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
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
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player == null || player.Folded || player.Hole.Count != 2 ||
                !context.Pots.Any(p => p.Chips > 0 && p.Winners.Contains(accountId))) return;
            var community = context.Streets[context.Streets.Count - 1].Community;
            if (community.Count < 3 || community.Count > 5) return;
            var rank = AchievementPatternEvaluator.GetBest(community.Concat(player.Hole).ToArray()).Rank;
            var currentStreet = context.PreviousStage;
            if (!currentStreet.HasValue) return;
            var foldedThisStreet = context.Actions.Where(action => action.Stage == currentStreet.Value &&
                    action.Type == PlayerOperationType.Fold)
                .Select(action => action.AccountId)
                .ToHashSet();
            foreach (var opponent in context.Players.Where(p => p.AccountId != accountId && p.Folded &&
                p.Hole.Count == 2 && foldedThisStreet.Contains(p.AccountId)))
            {
                // Folded players deliberately have no cached BestRank in the stage snapshot.
                // Compare only players who folded during the final betting street against
                // the same final, actually dealt board.
                var otherRank = AchievementPatternEvaluator.GetBest(community.Concat(opponent.Hole).ToArray()).Rank;
                if ((int)otherRank - (int)rank >= 2)
                {
                    progress.Count = Target;
                    return;
                }
            }
        }
    }

    public sealed class CallBluffAchievement : HandAchievement
    {
        public override string Id => "call_bluff";
        public override string Name => "虚张声势";
        public override string Description => "跟上对手全下，摊牌时以高出至少两级的牌型赢下双方争夺的奖池。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/CallBluff-v1";
        public override int SortOrder => 308;
        public override Reward Reward => new Reward(10000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player == null || player.Folded || !player.BestRank.HasValue) return;
            foreach (var opponent in context.Players.Where(p => p.AccountId != accountId && !p.Folded && p.BestRank.HasValue))
            {
                if ((int)player.BestRank.Value - (int)opponent.BestRank.Value < 2 ||
                    !context.Pots.Any(p => p.Chips > 0 && p.Winners.Contains(accountId) &&
                        p.Eligible.Contains(accountId) && p.Eligible.Contains(opponent.AccountId))) continue;
                if (RespondedToAllIn(context, accountId, opponent.AccountId))
                {
                    progress.Count = Target;
                    return;
                }
            }
        }

        private static bool RespondedToAllIn(AchievementContext context, string accountId, string opponentId)
        {
            foreach (var allIn in context.Actions.Where(a => a.AccountId == opponentId && IsAllIn(a)))
            {
                foreach (var response in context.Actions.Where(a => a.AccountId == accountId && !a.Disconnected &&
                    a.Stage == allIn.Stage && a.TurnId > allIn.TurnId &&
                    (a.Type == PlayerOperationType.Call || IsAllIn(a))))
                {
                    // StreetBet is cumulative. A later side-pot action must not be mistaken for
                    // calling a short all-in that this player had already covered beforehand.
                    var priorBet = context.Actions.Where(a => a.AccountId == accountId &&
                            a.Stage == response.Stage && a.TurnId < response.TurnId)
                        .Select(a => a.StreetBet)
                        .Concat(context.Streets.Where(s => s.Stage == response.Stage)
                            .SelectMany(s => s.Players).Where(p => p.AccountId == accountId).Select(p => p.StreetBet))
                        .DefaultIfEmpty(0u).Max();
                    if (priorBet < allIn.StreetBet && response.StreetBet > priorBet &&
                        (response.StreetBet >= allIn.StreetBet || response.RemainingChips == 0)) return true;
                }
            }
            return false;
        }

        private static bool IsAllIn(AchievementAction action)
        {
            return !action.Disconnected && action.RemainingChips == 0 && action.StreetBet > 0 &&
                (action.Type == PlayerOperationType.AllIn || action.Type == PlayerOperationType.Call ||
                    action.Type == PlayerOperationType.Bet || action.Type == PlayerOperationType.Raise);
        }
    }
}
