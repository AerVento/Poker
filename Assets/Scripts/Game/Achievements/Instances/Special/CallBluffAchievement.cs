using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes after calling a later all-in and winning their jointly contested pot by two categories.</summary>
    public sealed class CallBluffAchievement : HandAchievement
    {
        public override string Id => "call_bluff";
        public override string Name => "虚张声势";
        public override string Description => "跟上对手全下，摊牌时以高出至少两级的牌型赢下双方争夺的奖池。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/call-bluff";
        public override int SortOrder => 308;
        public override Reward Reward => new Reward(10000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player == null || player.Folded || !player.BestRank.HasValue) return;
            foreach (var opponent in context.Players.Where(opponent => opponent.AccountId != accountId && !opponent.Folded && opponent.BestRank.HasValue))
            {
                if ((int)player.BestRank.Value - (int)opponent.BestRank.Value < 2 ||
                    !context.Pots.Any(pot => pot.Chips > 0 && pot.Winners.Contains(accountId) &&
                        pot.Eligible.Contains(accountId) && pot.Eligible.Contains(opponent.AccountId))) continue;
                if (RespondedToAllIn(context, accountId, opponent.AccountId))
                {
                    progress.Count = Target;
                    return;
                }
            }
        }

        private static bool RespondedToAllIn(AchievementContext context, string accountId, string opponentId)
        {
            foreach (var allIn in context.Actions.Where(action => action.AccountId == opponentId && IsAllIn(action)))
            foreach (var response in context.Actions.Where(action => action.AccountId == accountId && !action.Disconnected &&
                action.Stage == allIn.Stage && action.TurnId > allIn.TurnId &&
                (action.Type == PlayerOperationType.Call || IsAllIn(action))))
            {
                // StreetBet is cumulative, so require the response to add chips beyond the player's prior commitment.
                var priorBet = context.Actions.Where(action => action.AccountId == accountId && action.Stage == response.Stage &&
                        action.TurnId < response.TurnId).Select(action => action.StreetBet)
                    .Concat(context.Streets.Where(street => street.Stage == response.Stage).SelectMany(street => street.Players)
                        .Where(snapshot => snapshot.AccountId == accountId).Select(snapshot => snapshot.StreetBet)).DefaultIfEmpty(0u).Max();
                if (priorBet < allIn.StreetBet && response.StreetBet > priorBet &&
                    (response.StreetBet >= allIn.StreetBet || response.RemainingChips == 0)) return true;
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
