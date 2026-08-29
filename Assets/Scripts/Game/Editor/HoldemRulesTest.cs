using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class HoldemRulesTest
    {
        [Test]
        public void GetPositions_Multiway_ActsLeftOfBigBlindPreflop()
        {
            var positions = HoldemRules.GetPositions(
                dealer: 0,
                new bool[] { true, true, true, true });

            Assert.AreEqual(1, positions.SmallBlind);
            Assert.AreEqual(2, positions.BigBlind);
            Assert.AreEqual(3, positions.FirstPreflopPlayer);
            Assert.AreEqual(1, positions.FirstPostflopPlayer);
        }

        [Test]
        public void GetPositions_Multiway_AllowsDeadButtonSeat()
        {
            var positions = HoldemRules.GetPositions(
                dealer: 1,
                new bool[] { true, false, true, true });

            Assert.AreEqual(2, positions.SmallBlind);
            Assert.AreEqual(3, positions.BigBlind);
            Assert.AreEqual(0, positions.FirstPreflopPlayer);
            Assert.AreEqual(2, positions.FirstPostflopPlayer);
        }

        [Test]
        public void GetPositions_HeadsUp_ButtonPostsSmallBlindAndActsFirstPreflop()
        {
            var positions = HoldemRules.GetPositions(
                dealer: 2,
                new bool[] { true, false, true, false });

            Assert.AreEqual(2, positions.SmallBlind);
            Assert.AreEqual(0, positions.BigBlind);
            Assert.AreEqual(2, positions.FirstPreflopPlayer);
            Assert.AreEqual(0, positions.FirstPostflopPlayer);
        }

        [Test]
        public void CanRaise_SingleShortAllIn_DoesNotReopenAction()
        {
            Assert.IsFalse(HoldemRules.CanRaise(
                hasActed: true,
                betFacedAtLastAction: 300,
                currentBet: 500,
                minimumRaiseIncrement: 300));
        }

        [Test]
        public void CanRaise_CumulativeShortAllIns_ReopenActionAtFullRaiseAmount()
        {
            Assert.IsTrue(HoldemRules.CanRaise(
                hasActed: true,
                betFacedAtLastAction: 300,
                currentBet: 600,
                minimumRaiseIncrement: 300));
        }

        [Test]
        public void CanRaise_PlayerWhoHasNotActed_CanRaiseShortAllIn()
        {
            Assert.IsTrue(HoldemRules.CanRaise(
                hasActed: false,
                betFacedAtLastAction: 0,
                currentBet: 50,
                minimumRaiseIncrement: 100));
        }

        [Test]
        public void GetMinimumRaiseTotal_AddsLastFullRaiseToCurrentWager()
        {
            Assert.AreEqual(1100, HoldemRules.GetMinimumRaiseTotal(800, 300));
        }
    }
}
