using System.Linq;
using Game.Events;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes immediately when AK is committed all-in before the flop.</summary>
    public sealed class AceKingAllInAchievement : Achievement, IAchievementHandler<HandActionRecordedEvent>
    {
        public override string Id => "ace_king_preflop_all_in";
        public override string Name => "AK不梭是棒槌";
        public override string Description => "手持 AK，在翻牌前全押。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/cards";
        public override int SortOrder => 303;
        public override Reward Reward => new Reward(5000);

        public void Evaluate(HandActionRecordedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            var action = playerEvent.Action;
            if (playerEvent.AccountId != accountId || action == null || action.Disconnected ||
                action.Stage != GameStage.Preflop || action.RemainingChips != 0 || action.StreetBet == 0) return;
            if (action.Type != PlayerOperationType.AllIn && action.Type != PlayerOperationType.Call &&
                action.Type != PlayerOperationType.Bet && action.Type != PlayerOperationType.Raise) return;
            if (playerEvent.Hole.Count == 2 && playerEvent.Hole.Any(card => card.Rank == PokerRank.Ace) &&
                playerEvent.Hole.Any(card => card.Rank == PokerRank.King)) progress.Count = Target;
        }
    }
}
