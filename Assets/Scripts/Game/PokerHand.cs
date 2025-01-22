using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Game
{
    public enum PokerHandRank
    {
        /// <summary>
        /// 高牌
        /// </summary>
        HighCard,

        /// <summary>
        /// 一对
        /// </summary>
        Pair,

        /// <summary>
        /// 两对
        /// </summary>
        Two_Pair,

        /// <summary>
        /// 三条
        /// </summary>
        Three_of_a_Kind,

        /// <summary>
        /// 顺子
        /// </summary>
        Straight,

        /// <summary>
        /// 同花
        /// </summary>
        Flush,

        /// <summary>
        /// 葫芦
        /// </summary>
        Full_House,

        /// <summary>
        /// 四条
        /// </summary>
        Four_of_a_Kind,

        /// <summary>
        /// 同花顺
        /// </summary>
        Straight_Flush,
    }

    /// <summary>
    /// 由五张牌构成的牌组
    /// </summary>
    [System.Serializable]
    public struct PokerHand
    {
        private delegate bool CheckPattern(out PokerPattern pattern);

        public Poker[] Pokers;

        public static PokerHand Create(params Poker[] pokers)
        {
            if (pokers.Length != 5)
                throw new ArgumentException($"The length of array {pokers} is not 5.", "pokers");
            PokerHand hand = new PokerHand();
            hand.Pokers = pokers;
            return hand;
        }

        public static PokerHand Create(List<Poker> pokers)
        {
            if (pokers.Count != 5)
                throw new ArgumentException($"The length of list {pokers} is not 5.", "pokers");
            PokerHand hand = new PokerHand();
            hand.Pokers = pokers.ToArray();
            return hand;
        }

        public PokerPattern GetPattern()
        {
            var pattern = HighCard();
            var list = new List<CheckPattern>()
            {
                CheckPair, CheckTwoPair, CheckThree_of_a_Kind, CheckStraight, CheckFlush, CheckFullHouse, CheckFour_of_a_Kind, CheckStraightFlush
            };
            for(int i = 0; i < list.Count; i++)
            {
                if (list[i].Invoke(out var newPattern))
                {
                    pattern = newPattern;
                }
            }
            return pattern;
        }

        private PokerPattern HighCard()
        {
            var pattern = new PokerPattern();
            pattern.Rank = PokerHandRank.HighCard;
            pattern.Grades = Pokers.OrderByDescending((poker) => poker.Rank).Select((poker) => (int)poker.Rank).ToList();
            return pattern;
        }

        private bool CheckPair(out PokerPattern pattern)
        {
            pattern = new PokerPattern();
            pattern.Rank = PokerHandRank.Pair;
            pattern.Grades = new List<int>();
            int[] count = new int[13];
            foreach (var poker in Pokers)
                count[(int)poker.Rank]++;

            // 有多少种牌有2张
            int two_count = 0;
            // 有多少种牌有1张
            int one_count = 0;
            // 倒序
            for (int i = count.Length - 1; i >= 0; i--)
                if (count[i] == 0)
                    continue;
                else if (!(count[i] == 1 || count[i] == 2)) // 一对只会出现数量为2和1的牌
                    return false;
                else if (count[i] == 1)
                {
                    one_count++;
                    pattern.Grades.Add(i);
                }
                else if (count[i] == 2)
                {
                    two_count++;
                    pattern.Grades.Insert(0, i); // 保证2张的牌的等级永远在1张的牌等级的前面
                }
            return two_count == 1 && one_count == 3;
        }

        private bool CheckTwoPair(out PokerPattern pattern)
        {
            pattern = new PokerPattern();
            pattern.Rank = PokerHandRank.Two_Pair;
            pattern.Grades = new List<int>();
            int[] count = new int[13];
            foreach (var poker in Pokers)
                count[(int)poker.Rank]++;

            // 有多少种牌有2张
            int two_count = 0;
            // 有多少种牌有1张
            int one_count = 0;

            // 正序
            for (int i = 0; i < count.Length; i++)
                if (count[i] == 0)
                    continue;
                else if (!(count[i] == 1 || count[i] == 2)) // 两对只会出现数量为2和1的牌
                    return false;
                else if (count[i] == 1)
                {
                    one_count++;
                    pattern.Grades.Add(i);
                }
                else if (count[i] == 2)
                {
                    two_count++;
                    pattern.Grades.Insert(0, i); // 保证2张的牌的等级永远在1张的牌等级的前面
                }
            return two_count == 2 && one_count == 1;
        }

        private bool CheckThree_of_a_Kind(out PokerPattern pattern)
        {
            pattern = new PokerPattern();
            pattern.Rank = PokerHandRank.Three_of_a_Kind;
            pattern.Grades = new List<int>();
            int[] count = new int[13];
            foreach (var poker in Pokers)
                count[(int)poker.Rank]++;

            // 有多少种牌有3张
            int three_count = 0;
            // 有多少种牌有1张
            int one_count = 0;

            // 倒序
            for (int i = count.Length - 1; i >= 0; i--)
                if (count[i] == 0)
                    continue;
                else if (!(count[i] == 1 || count[i] == 3)) // 三条只会出现数量为1和3的牌
                    return false;
                else if (count[i] == 1)
                {
                    one_count++;
                    pattern.Grades.Add(i);
                }
                else if (count[i] == 3)
                {
                    three_count++;
                    pattern.Grades.Insert(0, i); // 保证3张的牌的等级永远在1张的牌等级的前面
                }
            return three_count == 1 && one_count == 2;
        }

        private bool CheckStraight(out PokerPattern pattern)
        {
            pattern = new PokerPattern();

            Poker NextPoker(Poker poker)
            {
                if (poker.Rank == PokerRank.Ace)
                    return new Poker() { Suit = poker.Suit, Rank = PokerRank.Two };
                return new Poker() { Suit = poker.Suit, Rank = (PokerRank)((int)poker.Rank + 1) };
            }

            List<Poker> list = new List<Poker>(Pokers);
            list.Sort((a, b) => a.Rank - b.Rank);

            // 处理特殊情况: A,2,3,4,5组成的对子
            if (list[0].Rank == PokerRank.Two && list[1].Rank == PokerRank.Three && list[2].Rank == PokerRank.Four && list[3].Rank == PokerRank.Five && list[4].Rank == PokerRank.Ace)
            {
                pattern.Rank = PokerHandRank.Straight;
                pattern.Grades = new List<int>() { (int)PokerRank.Five };
                return true;
            }

            for (int i = 0; i < 4; i++)
            {
                if (!NextPoker(list[i]).SameTo(list[i + 1]))
                    return false;
            }

            pattern.Rank = PokerHandRank.Straight;
            pattern.Grades = new List<int>() { (int)list[4].Rank };
            return true;
        }

        private bool CheckFlush(out PokerPattern pattern)
        {
            pattern = new PokerPattern();
            PokerSuit suit = Pokers[0].Suit;
            for (int i = 1; i < Pokers.Length; i++)
            {
                if (suit != Pokers[i].Suit)
                    return false;
            }

            pattern.Rank = PokerHandRank.Flush;
            pattern.Grades = Pokers.OrderByDescending((poker) => poker.Rank).Select((poker) => (int)poker.Rank).ToList();
            return true;
        }

        private bool CheckFullHouse(out PokerPattern pattern)
        {
            pattern = new PokerPattern();
            pattern.Rank = PokerHandRank.Full_House;
            pattern.Grades = new List<int>();
            int[] count = new int[13];
            foreach (var poker in Pokers)
                count[(int)poker.Rank]++;

            // 有多少种牌有3张
            int three_count = 0;
            // 有多少种牌有2张
            int two_count = 0;
            for (int i = 0; i < count.Length; i++)
                if (count[i] == 0)
                    continue;
                else if (count[i] < 2 || count[i] > 3)
                    return false;
                else if(count[i] == 2)
                {
                    two_count++;
                    pattern.Grades.Add(i);
                }
                else if (count[i] == 3)
                {
                    three_count++;
                    pattern.Grades.Insert(0, i); // 保证3张的牌的等级永远在2张的牌等级的前面
                }
            return two_count == 1 && three_count == 1;
        }

        private bool CheckFour_of_a_Kind(out PokerPattern pattern)
        {
            pattern = new PokerPattern();
            int[] count = new int[13];
            foreach (var poker in Pokers)
                count[(int)poker.Rank]++;
            for(int i = 0; i < count.Length; i++)
                if (count[i] == 4)
                {
                    // 四条的等级：四张的等级和一张的等级
                    pattern.Rank = PokerHandRank.Four_of_a_Kind;
                    pattern.Grades = new List<int>() { i };
                    // 找剩下一张的等级
                    foreach(var poker in Pokers)
                        if((int)poker.Rank != i)
                        {
                            pattern.Grades.Add((int)poker.Rank);
                            break;
                        }
                    return true;
                }
            return false;
        }

        private bool CheckStraightFlush(out PokerPattern pattern)
        {
            pattern = new PokerPattern();
            if (CheckFlush(out var _) && CheckStraight(out var straight))
            {
                pattern.Rank = PokerHandRank.Straight_Flush;
                pattern.Grades = new List<int> { straight.Grades[0] };
                return true;
            }
            return false;
        }
    }
}
