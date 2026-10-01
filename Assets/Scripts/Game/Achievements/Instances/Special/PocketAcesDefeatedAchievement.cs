using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes after a heavily invested pocket-ace hand is folded or wins no pot.</summary>
    public sealed class PocketAcesDefeatedAchievement : HandAchievement
    {
        public override string Id => "pocket_aces_defeated";
        public override string Name => "AA的一万种死法";
        public override string Description => "手持 AA，翻牌前投入至少 $2,000，最终弃牌或一池未得。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/cards";
        public override int SortOrder => 304;
        public override Reward Reward => new Reward(2000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver) return;
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player == null || player.Hole.Count != 2 || player.Hole.Any(card => card.Rank != PokerRank.Ace)) return;
            var preflopBets = context.Actions.Where(action => action.AccountId == accountId && action.Stage == GameStage.Preflop)
                .Select(action => action.StreetBet).Concat(context.Streets.Where(street => street.Stage == GameStage.Preflop)
                    .SelectMany(street => street.Players).Where(snapshot => snapshot.AccountId == accountId).Select(snapshot => snapshot.StreetBet));
            if (preflopBets.DefaultIfEmpty(0u).Max() < 2000) return;
            if (player.Folded || !context.Pots.Any(pot => pot.Chips > 0 && pot.Winners.Contains(accountId))) progress.Count = Target;
        }
    }
}
