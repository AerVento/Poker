using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    public sealed class FullHouseBeatsFullHouseAchievement : HandAchievement
    {
        public override string Id => "full_house_beats_full_house";
        public override string Name => "葫芦之上";
        public override string Description => "摊牌时，用葫芦击败同一奖池中另一位持有葫芦的玩家。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/FullHouse";
        public override Reward Reward => new Reward(1000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player == null || player.Folded || player.BestRank != PokerHandRank.Full_House) return;
            foreach (var pot in context.Pots.Where(p => p.Chips > 0 && p.Winners.Contains(accountId)))
            {
                if (context.Players.Any(other => other.AccountId != accountId && !other.Folded &&
                    other.BestRank == PokerHandRank.Full_House && pot.Eligible.Contains(other.AccountId) &&
                    !pot.Winners.Contains(other.AccountId) && player.Beats(other)))
                {
                    progress.Count = Target;
                    return;
                }
            }
        }
    }
}
