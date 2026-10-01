using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game
{
    public enum PokerSuit
    {
        /// <summary>
        /// 红桃
        /// </summary>
        Heart,
        /// <summary>
        /// 方片
        /// </summary>
        Diamond,
        /// <summary>
        /// 梅花
        /// </summary>
        Club,
        /// <summary>
        /// 黑桃
        /// </summary>
        Spade,
    }

    public enum PokerRank
    {
        Two,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        Ten,
        Jack,
        Queen,
        King,
        Ace,
    }

    [System.Serializable]
    public struct Poker
    {
        public Poker(PokerSuit suit, PokerRank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public PokerSuit Suit { get; set; }
        public PokerRank Rank { get; set; }

        public static List<Poker> GetAll()
        {
            var list = new List<Poker>();
            for(PokerRank i = PokerRank.Two; i <= PokerRank.Ace; i++)
            {
                list.Add(new Poker(PokerSuit.Heart, i));
                list.Add(new Poker(PokerSuit.Diamond, i));
                list.Add(new Poker(PokerSuit.Club, i));
                list.Add(new Poker(PokerSuit.Spade, i));
            }
            return list;
        }

        public static Queue<Poker> RandomDeck()
        {
            var all = GetAll();
            Random r = new Random();
            for(int i = all.Count - 1; i >= 0; i--)
            {
                int j = r.Next(i + 1);
                (all[j], all[i]) = (all[i], all[j]);
            }
            return new Queue<Poker>(all);
        }

        public override string ToString()
        {
            string suit = Suit switch
            {
                PokerSuit.Heart => "♥",
                PokerSuit.Diamond => "♦",
                PokerSuit.Club => "♣",
                PokerSuit.Spade => "♠",
                _ => ""
            };
            string rank = Rank switch
            {
                PokerRank.Two => "2",
                PokerRank.Three => "3",
                PokerRank.Four => "4",
                PokerRank.Five => "5",
                PokerRank.Six => "6",
                PokerRank.Seven => "7",
                PokerRank.Eight => "8",
                PokerRank.Nine => "9",
                PokerRank.Ten => "10",
                PokerRank.Jack => "J",
                PokerRank.Queen => "Q",
                PokerRank.King => "K",
                PokerRank.Ace => "A",
                _ => ""
            };
            return suit + rank;
        }

        public bool BiggerThan(Poker other)
        {
            return this.Rank > other.Rank;
        }
        
        public bool SmallerThan(Poker other)
        { 
            return this.Rank < other.Rank;
        }

        public bool SameTo(Poker other)
        {
            return this.Rank == other.Rank;
        }

        public bool Is(Poker other)
        {
            return this.Suit == other.Suit && this.Rank == other.Rank;
        }
        public bool IsNot(Poker other)
        {
            return !Is(other);
        }
    }
}
