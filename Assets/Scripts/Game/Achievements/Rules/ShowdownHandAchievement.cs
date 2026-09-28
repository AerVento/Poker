using System.Linq;
using Game.Events;
using Game.Network;

namespace Game.Achievements
{
    /// <summary>Shared evaluator for achievements awarded for an exact revealed hand category.</summary>
    public abstract class ShowdownHandAchievement : HandAchievement, IAchievementHandler<HandCardsRevealedEvent>
    {
        public override string Category => AchievementCategories.Hand;
        public override string IconPath => "UI/Achievements/Cards-v1";
        protected abstract PokerHandRank Rank { get; }

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player != null && !player.Folded && player.BestRank == Rank) progress.Count = Target;
        }

        public void Evaluate(HandCardsRevealedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            if (playerEvent.AccountId != accountId || playerEvent.Hole.Count != 2 ||
                playerEvent.Community.Count < 3 || playerEvent.Community.Count > 5) return;
            if (AchievementPatternEvaluator.GetBest(playerEvent.Community.Concat(playerEvent.Hole).ToArray()).Rank == Rank)
                progress.Count = Target;
        }
    }
}
