using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game
{
    public static class PokerUtility
    {
        public static PokerPattern GetBiggestPattern(List<Poker> pokers, out Poker[] maxHand)
        {
            if (pokers.Count != 7)
                throw new ArgumentException();

            PokerPattern? maxPattern = null;
            // 遍历所有的可能牌型，然后选出最大的

            maxHand = new Poker[5];

            var count = pokers.Count;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    var hand = pokers
                        .Select((poker, index) => (poker, index))
                        .Where(pair => pair.index != i && pair.index != j)
                        .Select(pair => pair.poker);
                    var pokerHand = PokerHand.Create(hand.ToList());
                    var pattern = pokerHand.GetPattern();
                    if (maxPattern == null || pattern.BiggerThan(maxPattern.Value))
                    {
                        maxPattern = pattern;
                        maxHand = hand.ToArray();
                    }
                }
            }

            // 一定会有高牌，所以这个肯定有值
            return maxPattern.Value;
        }
    }
}
