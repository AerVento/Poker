using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Facts
{
    [Flags]
    public enum FormedPattern
    {
        None = 0, Pair = 1, TwoPair = 2, ThreeOfAKind = 4, Straight = 8,
        Flush = 16, FullHouse = 32, FourOfAKind = 64, StraightFlush = 128
    }

    public enum HandFactKind { PatternObserved, Settled, CardsRevealed }

    // Server-only facts. No task IDs, rewards, or player task state belong here.
    public sealed class HandFact
    {
        public string EventId { get; set; }
        public string AccountId { get; set; }
        public string HandId { get; set; }
        public HandFactKind Kind { get; set; }
        public int Street { get; set; }
        public FormedPattern Patterns { get; set; }
        public bool WonAnyPot { get; set; }
    }

    public interface IHandFactPublisher
    {
        void PublishHandFact(HandFact fact);
    }

    public static class FormablePatternEvaluator
    {
        public static FormedPattern Evaluate(IEnumerable<Poker> source)
        {
            var cards = source?.ToArray() ?? throw new ArgumentNullException(nameof(source));
            if ((cards.Length != 2 && (cards.Length < 5 || cards.Length > 7)) ||
                cards.Any(c => !Enum.IsDefined(typeof(PokerRank), c.Rank) || !Enum.IsDefined(typeof(PokerSuit), c.Suit)) ||
                cards.Select(c => ((int)c.Suit * 13) + (int)c.Rank).Distinct().Count() != cards.Length)
                throw new ArgumentException("Expected two hole cards or five to seven distinct legal cards.");
            var counts = cards.GroupBy(c => c.Rank).Select(g => g.Count()).ToArray();
            var result = counts.Any(n => n >= 2) ? FormedPattern.Pair : FormedPattern.None;
            if (cards.Length == 2) return result;
            if (counts.Count(n => n >= 2) >= 2) result |= FormedPattern.TwoPair;
            if (counts.Any(n => n >= 3)) result |= FormedPattern.ThreeOfAKind;
            if (counts.Any(n => n >= 4)) result |= FormedPattern.FourOfAKind;
            if (counts.Any(n => n >= 3) && counts.Count(n => n >= 2) >= 2) result |= FormedPattern.FullHouse;
            if (HasStraight(cards)) result |= FormedPattern.Straight;
            foreach (var suit in cards.GroupBy(c => c.Suit).Where(g => g.Count() >= 5))
            {
                result |= FormedPattern.Flush;
                if (HasStraight(suit)) result |= FormedPattern.StraightFlush;
            }
            return result;
        }

        private static bool HasStraight(IEnumerable<Poker> cards)
        {
            var ranks = new HashSet<int>(cards.Select(c => (int)c.Rank + 2));
            if (ranks.Contains(14)) ranks.Add(1);
            return Enumerable.Range(1, 10).Any(low => Enumerable.Range(low, 5).All(ranks.Contains));
        }
    }
}
