using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Achievements
{
    public static class AchievementPatternEvaluator
    {
        public static PokerPattern GetBest(IReadOnlyList<Poker> cards)
        {
            if (cards == null || cards.Count < 5 || cards.Count > 7)
                throw new ArgumentException("A board snapshot needs five to seven cards.", nameof(cards));
            if (cards.Count == 7) return PokerUtility.GetBiggestPattern(cards.ToList(), out _);
            if (cards.Count == 5) return PokerHand.Create(cards.ToList()).GetPattern();
            PokerPattern? best = null;
            for (var omitted = 0; omitted < cards.Count; omitted++)
            {
                var pattern = PokerHand.Create(cards.Where((card, index) => index != omitted).ToList()).GetPattern();
                if (!best.HasValue || pattern.BiggerThan(best.Value)) best = pattern;
            }
            return best.Value;
        }
    }
}
