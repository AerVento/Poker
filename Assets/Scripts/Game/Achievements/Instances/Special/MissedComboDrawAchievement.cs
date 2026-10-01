using System.Collections.Generic;
using System.Linq;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    /// <summary>Completes when an existing flop or turn combo draw misses at showdown without winning a pot.</summary>
    public sealed class MissedComboDrawAchievement : HandAchievement
    {
        public override string Id => "missed_combo_draw";
        public override string Name => "大三喜小二元";
        public override string Description => "摊牌时，听顺又听花，双双落空且一池未得。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/full-house";
        public override int SortOrder => 305;
        public override Reward Reward => new Reward(5000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(player => player.AccountId == accountId);
            if (player == null || player.Folded || player.Hole.Count != 2 ||
                context.Pots.Any(pot => pot.Chips > 0 && pot.Winners.Contains(accountId))) return;
            var community = context.Streets[context.Streets.Count - 1].Community;
            if (community.Count != 5 || !HasComboDraw(community.Concat(player.Hole))) return;

            // River must not create the opportunity: it must exist on the flop or turn snapshot.
            if (context.Streets.Any(street => (street.Stage == GameStage.Flop || street.Stage == GameStage.Turn) &&
                HasComboDraw(street.Community.Concat(player.Hole)))) progress.Count = Target;
        }

        private static bool HasComboDraw(IEnumerable<Poker> cards)
        {
            var allCards = cards.ToArray();
            return allCards.GroupBy(card => card.Suit).Max(group => group.Count()) == 4 && HasNearStraightWithoutStraight(allCards);
        }

        private static bool HasNearStraightWithoutStraight(IReadOnlyList<Poker> cards)
        {
            var ranks = cards.Select(card => (int)card.Rank).ToHashSet();
            if (ranks.Contains((int)PokerRank.Ace)) ranks.Add(-1);
            var nearStraight = false;
            for (var start = -1; start <= (int)PokerRank.Ten; start++)
            {
                var count = Enumerable.Range(start, 5).Count(ranks.Contains);
                if (count == 5) return false;
                if (count == 4) nearStraight = true;
            }
            return nearStraight;
        }
    }
}
