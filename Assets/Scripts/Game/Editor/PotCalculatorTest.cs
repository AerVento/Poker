using Game.Network;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Tests
{
    // 以下案例的期望值依据 Poker TDA Rules 20/21 人工计算，
    // 而不是由 PotCalculator 自身生成。
    public class PotCalculatorTest
    {
        [Test]
        public void Calculate_EqualContributions_CreatesOnePot()
        {
            var result = PotCalculator.Calculate(
                new uint[] { 100, 100, 100 },
                new bool[] { false, false, false });

            Assert.AreEqual(1, result.Pots.Count);
            AssertPot(result.Pots[0], 300, 0, 1, 2);
            CollectionAssert.AreEqual(new uint[] { 0, 0, 0 }, result.Refunds);
        }

        [Test]
        public void Calculate_OneShortAllIn_CreatesMainAndSidePot()
        {
            var result = PotCalculator.Calculate(
                new uint[] { 50, 150, 150 },
                new bool[] { false, false, false });

            Assert.AreEqual(2, result.Pots.Count);
            AssertPot(result.Pots[0], 150, 0, 1, 2);
            AssertPot(result.Pots[1], 200, 1, 2);
        }

        [Test]
        public void Calculate_LaterStreetBets_StayOutsideShortStacksMainPot()
        {
            var result = PotCalculator.Calculate(
                new uint[] { 50, 250, 250 },
                new bool[] { false, false, false });

            Assert.AreEqual(2, result.Pots.Count);
            AssertPot(result.Pots[0], 150, 0, 1, 2);
            AssertPot(result.Pots[1], 400, 1, 2);
            CollectionAssert.AreEqual(new uint[] { 0, 0, 0 }, result.Refunds);
        }

        [Test]
        public void Calculate_MultipleAllIns_CreatesOneLayerPerContributionLevel()
        {
            var result = PotCalculator.Calculate(
                new uint[] { 30, 80, 200, 200 },
                new bool[] { false, false, false, false });

            Assert.AreEqual(3, result.Pots.Count);
            AssertPot(result.Pots[0], 120, 0, 1, 2, 3);
            AssertPot(result.Pots[1], 150, 1, 2, 3);
            AssertPot(result.Pots[2], 240, 2, 3);
        }

        [Test]
        public void Calculate_FoldedPlayerContributesDeadMoneyButCannotWin()
        {
            var result = PotCalculator.Calculate(
                new uint[] { 50, 150, 150 },
                new bool[] { false, false, true });

            Assert.AreEqual(2, result.Pots.Count);
            AssertPot(result.Pots[0], 150, 0, 1);
            AssertPot(result.Pots[1], 200, 1);
        }

        [Test]
        public void Calculate_UncalledChips_AreRefundedInsteadOfCreatingOnePlayerPot()
        {
            var result = PotCalculator.Calculate(
                new uint[] { 200, 120 },
                new bool[] { false, false });

            Assert.AreEqual(1, result.Pots.Count);
            AssertPot(result.Pots[0], 240, 0, 1);
            CollectionAssert.AreEqual(new uint[] { 80, 0 }, result.Refunds);
        }

        [Test]
        public void Calculate_EnumeratedSmallStates_AlwaysConservesChips()
        {
            var values = new uint[] { 0, 1, 2, 5 };
            for (int playerCount = 2; playerCount <= 4; playerCount++)
            {
                var stateCount = (int)Math.Pow(values.Length, playerCount);
                for (int state = 0; state < stateCount; state++)
                {
                    var contributions = DecodeContributions(state, playerCount, values);
                    var highestContribution = contributions.Max();
                    for (int foldMask = 0; foldMask < 1 << playerCount; foldMask++)
                    {
                        var foldedPlayers = Enumerable.Range(0, playerCount)
                            .Select(index => (foldMask & (1 << index)) != 0)
                            .ToArray();

                        // 合法牌局的每一层奖池至少有一位未弃牌玩家。
                        if (!contributions
                            .Select((contribution, index) => (contribution, index))
                            .Any(pair => pair.contribution == highestContribution && !foldedPlayers[pair.index]))
                        {
                            continue;
                        }

                        var result = PotCalculator.Calculate(contributions, foldedPlayers);
                        var resultChipCount = result.Pots.Aggregate(0UL, (sum, pot) => sum + pot.ChipCount)
                            + result.Refunds.Aggregate(0UL, (sum, refund) => sum + refund);
                        var inputChipCount = contributions.Aggregate(0UL, (sum, contribution) => sum + contribution);

                        Assert.AreEqual(inputChipCount, resultChipCount);
                        Assert.IsTrue(result.Pots.All(pot => pot.EligiblePlayers.All(index => !foldedPlayers[index])));
                    }
                }
            }
        }

        [Test]
        public void GetOddChipOrder_StartsAtFirstWinnerLeftOfDealer()
        {
            var order = PotCalculator.GetOddChipOrder(new int[] { 0, 2, 3 }, dealer: 2, playerCount: 5);

            CollectionAssert.AreEqual(new int[] { 3, 0, 2 }, order);
        }

        private static uint[] DecodeContributions(int state, int playerCount, IReadOnlyList<uint> values)
        {
            var contributions = new uint[playerCount];
            for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                contributions[playerIndex] = values[state % values.Count];
                state /= values.Count;
            }
            return contributions;
        }

        private static void AssertPot(PotCalculator.Pot pot, uint chipCount, params int[] eligiblePlayers)
        {
            Assert.AreEqual(chipCount, pot.ChipCount);
            CollectionAssert.AreEqual(eligiblePlayers, pot.EligiblePlayers);
        }
    }
}
