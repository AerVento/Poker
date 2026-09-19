using System;
using System.Linq;
using Game.Events;
using Game.Network;
using Game.Tasks;

namespace Game.Achievements
{
    public sealed class AceKingAllInAchievement : Achievement, IAchievementHandler<HandActionRecordedEvent>
    {
        public override string Id => "ace_king_preflop_all_in";
        public override string Name => "AK不梭是棒槌";
        public override string Description => "手持 AK，在翻牌前全押。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/Cards-v1";
        public override int SortOrder => 303;
        public override Reward Reward => new Reward(5000);

        public void Evaluate(HandActionRecordedEvent playerEvent, string accountId, AchievementProgress progress)
        {
            var action = playerEvent.Action;
            if (playerEvent.AccountId != accountId || action == null || action.Disconnected ||
                action.Stage != GameStage.Preflop || action.RemainingChips != 0 || action.StreetBet == 0) return;
            if (action.Type != PlayerOperationType.AllIn && action.Type != PlayerOperationType.Call &&
                action.Type != PlayerOperationType.Bet && action.Type != PlayerOperationType.Raise) return;
            if (playerEvent.Hole.Count == 2 && playerEvent.Hole.Any(p => p.Rank == PokerRank.Ace) &&
                playerEvent.Hole.Any(p => p.Rank == PokerRank.King))
            {
                progress.Count = Target;
            }
        }
    }

    public sealed class PocketAcesDefeatedAchievement : HandAchievement
    {
        public override string Id => "pocket_aces_defeated";
        public override string Name => "AA的一万种死法";
        public override string Description => "手持 AA，翻牌前投入至少 $2,000，最终弃牌或一池未得。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/Cards-v1";
        public override int SortOrder => 304;
        public override Reward Reward => new Reward(2000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver) return;
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player == null || player.Hole.Count != 2 || player.Hole.Any(p => p.Rank != PokerRank.Ace)) return;
            var preflopBets = context.Actions.Where(a => a.AccountId == accountId && a.Stage == GameStage.Preflop)
                .Select(a => a.StreetBet).Concat(context.Streets.Where(s => s.Stage == GameStage.Preflop)
                    .SelectMany(s => s.Players).Where(p => p.AccountId == accountId).Select(p => p.StreetBet));
            if (preflopBets.DefaultIfEmpty(0u).Max() < 2000) return;
            if (player.Folded || !context.Pots.Any(p => p.Chips > 0 && p.Winners.Contains(accountId)))
            {
                progress.Count = Target;
            }
        }
    }

    public sealed class MissedComboDrawAchievement : HandAchievement
    {
        public override string Id => "missed_combo_draw";
        public override string Name => "大三喜小二元";
        public override string Description => "摊牌时，听顺又听花，双双落空且一池未得。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/FullHouse";
        public override int SortOrder => 305;
        public override Reward Reward => new Reward(5000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player == null || player.Folded || player.Hole.Count != 2 ||
                context.Pots.Any(p => p.Chips > 0 && p.Winners.Contains(accountId))) return;
            var community = context.Streets[context.Streets.Count - 1].Community;
            if (community.Count != 5) return;
            if (!HasComboDraw(community.Concat(player.Hole))) return;

            // A river card may leave the player on both draws, but must not create
            // the opportunity by itself. The hand must already have had both draws
            // at the end of either earlier post-flop betting street.
            if (context.Streets.Any(street =>
                    (street.Stage == GameStage.Flop || street.Stage == GameStage.Turn) &&
                    HasComboDraw(street.Community.Concat(player.Hole))))
            {
                progress.Count = Target;
            }
        }

        private static bool HasComboDraw(IEnumerable<Poker> cards)
        {
            var allCards = cards.ToArray();
            return allCards.GroupBy(card => card.Suit).Max(group => group.Count()) == 4 &&
                HasNearStraightWithoutStraight(allCards);
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

    public sealed class StrongestSplitPotAchievement : HandAchievement
    {
        public override string Id => "strongest_split_pot";
        public override string Name => "史上最强vs现代最强";
        public override string Description => "摊牌时，双方以葫芦或更强牌型平分奖池。";
        public override string Category => AchievementCategories.Special;
        public override string IconPath => "UI/Achievements/Champion-v1";
        public override int SortOrder => 306;
        public override Reward Reward => new Reward(5000);

        public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress)
        {
            if (context.Stage != GameStage.RoundOver || !context.Showdown) return;
            var player = context.Players.FirstOrDefault(p => p.AccountId == accountId);
            if (player == null || player.Folded || !player.BestRank.HasValue || player.BestRank.Value < PokerHandRank.Full_House) return;
            foreach (var pot in context.Pots.Where(p => p.Chips > 0 && p.Winners.Count == 2 && p.Winners.Contains(accountId)))
            {
                var other = context.Players.FirstOrDefault(p => p.AccountId != accountId && pot.Winners.Contains(p.AccountId));
                if (other != null && !other.Folded && other.BestRank.HasValue && other.BestRank.Value >= PokerHandRank.Full_House &&
                    !player.Beats(other) && !other.Beats(player))
                {
                    progress.Count = Target;
                    return;
                }
            }
        }
    }
}
