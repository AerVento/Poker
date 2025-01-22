using Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Tests
{
    public class PokerTester : MonoBehaviour
    {
        private void Start()
        {
            Test();
        }

        public void Test()
        {
            for(int i = 0; i < 1000; i++)
            {
                var deck = Poker.RandomDeck();
                List<Poker> community = new List<Poker>() { deck.Dequeue(), deck.Dequeue(), deck.Dequeue(), deck.Dequeue(), deck.Dequeue() };
                string ans = "Community: ";
                foreach(var poker in community)
                    ans += poker.ToString() + ", ";
                ans += "\n";

                List<Poker> holeA = new List<Poker>() { deck.Dequeue(), deck.Dequeue() };
                ans += $"Player A: ";
                foreach (var poker in holeA)
                    ans += poker.ToString() + ", ";
                ans += "; ";

                var maxPatternA = PokerUtility.GetBiggestPattern(community.Concat(holeA).ToList(), out _);
                ans += maxPatternA + "[";
                foreach (var grade in maxPatternA.Grades)
                    ans += ((PokerRank)grade).ToString() + ", ";
                ans += "]\n";

                List<Poker> holeB = new List<Poker>() { deck.Dequeue(), deck.Dequeue() };
                ans += $"Player 2: ";
                foreach (var poker in holeB)
                    ans += poker.ToString() + ", ";
                ans += "; ";

                var maxPatternB = PokerUtility.GetBiggestPattern(community.Concat(holeB).ToList(), out _);
                ans += maxPatternB + "[";
                foreach (var grade in maxPatternB.Grades)
                    ans += ((PokerRank)grade).ToString() + ", ";
                ans += "]\n";

                if (maxPatternA.BiggerThan(maxPatternB))
                    ans += "A大\n";
                if (maxPatternA.SmallerThan(maxPatternB))
                    ans += "B大\n";
                if (maxPatternA.SameTo(maxPatternB))
                    ans += "一样大\n";


                Debug.Log(ans);
            }


        }
    }
}
