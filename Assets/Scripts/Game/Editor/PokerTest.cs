using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Tests
{
    [TestFixture]
    public class PokerTest
    {
        [Test]
        public void PokerSizeTest1()
        {
            Poker a = new Poker(PokerSuit.Spade, PokerRank.Ace);
            Poker b = new Poker(PokerSuit.Diamond, PokerRank.Two);
            Assert.IsTrue(a.BiggerThan(b));
            Assert.IsFalse(a.SmallerThan(b));
        }

        [Test]
        public void PokerSizeTest2()
        {
            Poker a = new Poker(PokerSuit.Heart, PokerRank.Ten);
            Poker b = new Poker(PokerSuit.Club, PokerRank.King);
            Assert.IsTrue(a.SmallerThan(b));
            Assert.IsFalse(a.BiggerThan(b));
        }

        [Test]
        public void PokerSizeTest3()
        {
            Poker a = new Poker(PokerSuit.Spade, PokerRank.Queen);
            Poker b = new Poker(PokerSuit.Diamond, PokerRank.Queen);
            Assert.IsFalse(a.SmallerThan(b));
            Assert.IsFalse(a.BiggerThan(b));
            Assert.IsTrue(a.SameTo(b));
        }

        [Test]
        public void PokerHandTest1()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();
            Assert.AreEqual(PokerHandRank.HighCard, result.Rank);

            Assert.AreEqual((int)PokerRank.Ace, result.Grades[0]);
            Assert.AreEqual((int)PokerRank.King, result.Grades[1]);
            Assert.AreEqual((int)PokerRank.Queen, result.Grades[2]);
            Assert.AreEqual((int)PokerRank.Ten, result.Grades[3]);
            Assert.AreEqual((int)PokerRank.Two, result.Grades[4]);
        }

        [Test]
        public void PokerHandTest2()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Ace),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();
            Assert.AreEqual(PokerHandRank.Pair, result.Rank);

            Assert.AreEqual((int)PokerRank.Ace, result.Grades[0]);
            Assert.AreEqual((int)PokerRank.King, result.Grades[1]);
            Assert.AreEqual((int)PokerRank.Queen, result.Grades[2]);
            Assert.AreEqual((int)PokerRank.Ten, result.Grades[3]);
        }

        [Test]
        public void PokerHandTest3()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Two),
                new Poker(PokerSuit.Spade, PokerRank.Ten)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();
            Assert.AreEqual(PokerHandRank.Two_Pair, result.Rank);

            Assert.AreEqual((int)PokerRank.Ten, result.Grades[0]);
            Assert.AreEqual((int)PokerRank.Two, result.Grades[1]);
            Assert.AreEqual((int)PokerRank.Ace, result.Grades[2]);
        }


        [Test]
        public void PokerHandTest4()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.King)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();
            Assert.AreEqual(PokerHandRank.Three_of_a_Kind, result.Rank);

            Assert.AreEqual((int)PokerRank.King, result.Grades[0]);
            Assert.AreEqual((int)PokerRank.Ten, result.Grades[1]);
            Assert.AreEqual((int)PokerRank.Two, result.Grades[2]);
        }

        [Test]
        public void PokerHandTest5()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Three),
                new Poker(PokerSuit.Spade, PokerRank.Four),
                new Poker(PokerSuit.Club, PokerRank.Five)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();
            Assert.AreEqual(PokerHandRank.Straight, result.Rank);

            Assert.AreEqual((int)PokerRank.Five, result.Grades[0]);
        }

        [Test]
        public void PokerHandTest6()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Jack),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();
            Assert.AreEqual(PokerHandRank.Straight, result.Rank);

            Assert.AreEqual((int)PokerRank.Ace, result.Grades[0]);
        }

        [Test]
        public void PokerHandTest7()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Nine),
                new Poker(PokerSuit.Diamond, PokerRank.Jack),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.Eight)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();
            Assert.AreEqual(PokerHandRank.Straight, result.Rank);

            Assert.AreEqual((int)PokerRank.Queen, result.Grades[0]);
        }

        [Test]
        public void PokerHandTest8()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Five),
                new Poker(PokerSuit.Spade, PokerRank.Two),
                new Poker(PokerSuit.Spade, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();
            Assert.AreEqual(PokerHandRank.Flush, result.Rank);

            Assert.AreEqual((int)PokerRank.King, result.Grades[0]);
            Assert.AreEqual((int)PokerRank.Queen, result.Grades[1]);
            Assert.AreEqual((int)PokerRank.Ten, result.Grades[2]);
            Assert.AreEqual((int)PokerRank.Five, result.Grades[3]);
            Assert.AreEqual((int)PokerRank.Two, result.Grades[4]);
        }

        [Test]
        public void PokerHandTest9()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Club, PokerRank.Queen),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.Two)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();
            Assert.AreEqual(PokerHandRank.Full_House, result.Rank);

            Assert.AreEqual((int)PokerRank.Queen, result.Grades[0]);
            Assert.AreEqual((int)PokerRank.Two, result.Grades[1]);
        }

        [Test]
        public void PokerHandTest10()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Club, PokerRank.Eight),
                new Poker(PokerSuit.Diamond, PokerRank.Eight),
                new Poker(PokerSuit.Heart, PokerRank.Eight),
                new Poker(PokerSuit.Spade, PokerRank.Eight),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();
            Assert.AreEqual(PokerHandRank.Four_of_a_Kind, result.Rank);

            Assert.AreEqual((int)PokerRank.Eight, result.Grades[0]);
            Assert.AreEqual((int)PokerRank.King, result.Grades[1]);
        }

        [Test]
        public void PokerHandTest11()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Heart, PokerRank.Eight),
                new Poker(PokerSuit.Heart, PokerRank.Seven),
                new Poker(PokerSuit.Heart, PokerRank.Nine),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Heart, PokerRank.Six)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();

            Assert.AreEqual(PokerHandRank.Straight_Flush, result.Rank);
            Assert.AreEqual((int)PokerRank.Ten, result.Grades[0]);
        }

        [Test]
        public void PokerHandTest12()
        {
            Poker[] pokers = new Poker[]
            {
                new Poker(PokerSuit.Diamond, PokerRank.Ten),
                new Poker(PokerSuit.Diamond, PokerRank.Jack),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Queen)
            };
            PokerHand hand = PokerHand.Create(pokers);
            var result = hand.GetPattern();

            Assert.AreEqual(PokerHandRank.Straight_Flush, result.Rank);
            Assert.AreEqual((int)PokerRank.Ace, result.Grades[0]);
            Debug.Log(result.ToString());
        }

        [Test]
        public void PokerRankTest1()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Two),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 高牌小于一对
            Assert.IsTrue(result1.SmallerThan(result2));
        }

        [Test]
        public void PokerRankTest2()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Five),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Club, PokerRank.Seven),
                new Poker(PokerSuit.Spade, PokerRank.Jack)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 都是高牌，大的获胜
            Assert.IsTrue(result1.BiggerThan(result2));
        }

        [Test]
        public void PokerRankTest3()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Queen),
                new Poker(PokerSuit.Heart, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Five),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Seven),
                new Poker(PokerSuit.Club, PokerRank.Seven),
                new Poker(PokerSuit.Spade, PokerRank.Jack)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 都是一对，比较对子大小
            Assert.IsTrue(result1.BiggerThan(result2));
        }

        [Test]
        public void PokerRankTest4()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Two),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.Jack),
                new Poker(PokerSuit.Spade, PokerRank.Two)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 都是一对，对子一样大时，比较剩下来三张牌的大小
            Assert.IsTrue(result1.BiggerThan(result2));
        }

        [Test]
        public void PokerRankTest5()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Two),
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Jack),
                new Poker(PokerSuit.Club, PokerRank.Jack),
                new Poker(PokerSuit.Spade, PokerRank.Ace)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 两对比两个对子
            Assert.IsTrue(result1.SmallerThan(result2));
        }

        [Test]
        public void PokerRankTest6()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Two),
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Two),
                new Poker(PokerSuit.Club, PokerRank.Jack),
                new Poker(PokerSuit.Spade, PokerRank.Queen)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 两个对子一样时，比单张
            Assert.IsTrue(result1.BiggerThan(result2));
        }

        [Test]
        public void PokerRankTest7()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Queen),
                new Poker(PokerSuit.Diamond, PokerRank.Queen),
                new Poker(PokerSuit.Heart, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Queen),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Jack),
                new Poker(PokerSuit.Diamond, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Ace),
                new Poker(PokerSuit.Club, PokerRank.Jack),
                new Poker(PokerSuit.Diamond, PokerRank.Jack)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 三条
            Assert.IsTrue(result1.BiggerThan(result2));
        }


        [Test]
        public void PokerRankTest8()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Jack),
                new Poker(PokerSuit.Diamond, PokerRank.Jack),
                new Poker(PokerSuit.Heart, PokerRank.Two),
                new Poker(PokerSuit.Heart, PokerRank.Jack),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Jack),
                new Poker(PokerSuit.Diamond, PokerRank.Ten),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.Jack),
                new Poker(PokerSuit.Diamond, PokerRank.Jack)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 三条
            Assert.IsTrue(result1.SmallerThan(result2));
        }

        [Test]
        public void PokerRankTest9()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Jack),
                new Poker(PokerSuit.Diamond, PokerRank.Ten),
                new Poker(PokerSuit.Heart, PokerRank.Queen),
                new Poker(PokerSuit.Heart, PokerRank.Nine),
                new Poker(PokerSuit.Spade, PokerRank.King)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ten),
                new Poker(PokerSuit.Diamond, PokerRank.Ace),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.Jack),
                new Poker(PokerSuit.Diamond, PokerRank.Queen)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 顺子
            Assert.IsTrue(result1.SmallerThan(result2));
        }

        [Test]
        public void PokerRankTest10()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Spade, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Two),
                new Poker(PokerSuit.Spade, PokerRank.Five),
                new Poker(PokerSuit.Spade, PokerRank.Nine)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Spade, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Three),
                new Poker(PokerSuit.Spade, PokerRank.Five),
                new Poker(PokerSuit.Spade, PokerRank.Nine)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 同花
            Assert.IsTrue(result1.SmallerThan(result2));
        }

        [Test]
        public void PokerRankTest11()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Spade, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Two),
                new Poker(PokerSuit.Spade, PokerRank.Five),
                new Poker(PokerSuit.Spade, PokerRank.Nine)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Spade, PokerRank.Ten),
                new Poker(PokerSuit.Spade, PokerRank.Three),
                new Poker(PokerSuit.Spade, PokerRank.Five),
                new Poker(PokerSuit.Spade, PokerRank.Nine)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 同花
            Assert.IsTrue(result1.BiggerThan(result2));
        }

        [Test]
        public void PokerRankTest12()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Ace),
                new Poker(PokerSuit.Heart, PokerRank.Ace),
                new Poker(PokerSuit.Heart, PokerRank.Five),
                new Poker(PokerSuit.Club, PokerRank.Five)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.Six),
                new Poker(PokerSuit.Club, PokerRank.Six)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 葫芦
            Assert.IsTrue(result1.BiggerThan(result2));
        }

        [Test]
        public void PokerRankTest13()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.Five),
                new Poker(PokerSuit.Club, PokerRank.Five)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.Six),
                new Poker(PokerSuit.Club, PokerRank.Six)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 葫芦
            Assert.IsTrue(result1.SmallerThan(result2));
        }

        [Test]
        public void PokerRankTest14()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.Six),
                new Poker(PokerSuit.Club, PokerRank.Six)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.Six),
                new Poker(PokerSuit.Club, PokerRank.Six)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 葫芦
            Assert.IsTrue(result1.SameTo(result2));
        }

        [Test]
        public void PokerRankTest15()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.Queen)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Ace),
                new Poker(PokerSuit.Heart, PokerRank.Ace),
                new Poker(PokerSuit.Club, PokerRank.Ace),
                new Poker(PokerSuit.Club, PokerRank.Six)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 四条
            Assert.IsTrue(result1.SmallerThan(result2));
        }

        [Test]
        public void PokerRankTest16()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.Queen)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.Six)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 四条
            Assert.IsTrue(result1.BiggerThan(result2));
        }

        [Test]
        public void PokerRankTest17()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.Queen)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.King),
                new Poker(PokerSuit.Club, PokerRank.Queen)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 四条
            Assert.IsTrue(result1.SameTo(result2));

        }
        [Test]
        public void PokerRankTest18()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Diamond, PokerRank.Ten),
                new Poker(PokerSuit.Diamond, PokerRank.Jack),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.Ace),
                new Poker(PokerSuit.Diamond, PokerRank.Queen)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Diamond, PokerRank.Ten),
                new Poker(PokerSuit.Diamond, PokerRank.Jack),
                new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.Nine),
                new Poker(PokerSuit.Diamond, PokerRank.Queen)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 同花顺
            Assert.IsTrue(result1.BiggerThan(result2));
        }

        [Test]
        public void PokerRankTest19()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Two),
                new Poker(PokerSuit.Diamond, PokerRank.Ace),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.Five),
                new Poker(PokerSuit.Club, PokerRank.Nine)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Two),
                new Poker(PokerSuit.Diamond, PokerRank.Ace),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.Five),
                new Poker(PokerSuit.Club, PokerRank.Nine)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 高牌
            Assert.IsTrue(result1.SameTo(result2));
        }

        [Test]
        public void PokerRankTest20()
        {
            Poker[] pokers1 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Two),
                new Poker(PokerSuit.Diamond, PokerRank.Eight),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.Five),
                new Poker(PokerSuit.Club, PokerRank.Nine)
            };

            PokerHand hand1 = PokerHand.Create(pokers1);
            var result1 = hand1.GetPattern();

            Poker[] pokers2 = new Poker[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Two),
                new Poker(PokerSuit.Diamond, PokerRank.Ace),
                new Poker(PokerSuit.Heart, PokerRank.King),
                new Poker(PokerSuit.Diamond, PokerRank.Five),
                new Poker(PokerSuit.Club, PokerRank.Nine)
            };

            PokerHand hand2 = PokerHand.Create(pokers2);
            var result2 = hand2.GetPattern();

            // 高牌
            Assert.IsTrue(result1.SmallerThan(result2));
        }
    }
}
