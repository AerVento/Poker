using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Network
{
    /// <summary>
    /// 与 Unity 和网络层无关的无限注德州规则辅助函数。
    /// </summary>
    public static class HoldemRules
    {
        public sealed class Positions
        {
            public int Dealer { get; }

            public int SmallBlind { get; }

            public int BigBlind { get; }

            public int FirstPreflopPlayer { get; }

            public int FirstPostflopPlayer { get; }

            public Positions(
                int dealer,
                int smallBlind,
                int bigBlind,
                int firstPreflopPlayer,
                int firstPostflopPlayer)
            {
                Dealer = dealer;
                SmallBlind = smallBlind;
                BigBlind = bigBlind;
                FirstPreflopPlayer = firstPreflopPlayer;
                FirstPostflopPlayer = firstPostflopPlayer;
            }
        }

        public static bool CanRaise(
            bool hasActed,
            uint betFacedAtLastAction,
            uint currentBet,
            uint minimumRaiseIncrement)
        {
            if (!hasActed)
                return true;
            if (minimumRaiseIncrement == 0 || currentBet < betFacedAtLastAction)
                return false;

            return currentBet - betFacedAtLastAction >= minimumRaiseIncrement;
        }

        public static uint GetMinimumRaiseTotal(uint currentBet, uint minimumRaiseIncrement)
        {
            if (minimumRaiseIncrement == 0)
                throw new ArgumentOutOfRangeException(nameof(minimumRaiseIncrement));

            return checked(currentBet + minimumRaiseIncrement);
        }

        public static int GetNextSeat(int seat, IReadOnlyList<bool> participatingPlayers)
        {
            ValidateParticipants(participatingPlayers);
            if (seat < -1 || seat >= participatingPlayers.Count)
                throw new ArgumentOutOfRangeException(nameof(seat));

            for (int offset = 1; offset <= participatingPlayers.Count; offset++)
            {
                var candidate = (seat + offset) % participatingPlayers.Count;
                if (participatingPlayers[candidate])
                    return candidate;
            }

            throw new InvalidOperationException("No participating player was found.");
        }

        public static Positions GetPositions(int dealer, IReadOnlyList<bool> participatingPlayers)
        {
            ValidateParticipants(participatingPlayers);
            var participantCount = participatingPlayers.Count(participates => participates);
            if (participantCount < 2)
                throw new ArgumentException("At least two players must participate.", nameof(participatingPlayers));
            if (dealer < 0 || dealer >= participatingPlayers.Count)
                throw new ArgumentOutOfRangeException(nameof(dealer));

            if (participantCount == 2)
            {
                if (!participatingPlayers[dealer])
                    throw new ArgumentException("The heads-up dealer must participate in the hand.", nameof(dealer));
                var bigBlind = GetNextSeat(dealer, participatingPlayers);
                return new Positions(
                    dealer,
                    dealer,
                    bigBlind,
                    dealer,
                    bigBlind);
            }

            var smallBlind = GetNextSeat(dealer, participatingPlayers);
            var multiwayBigBlind = GetNextSeat(smallBlind, participatingPlayers);
            return new Positions(
                dealer,
                smallBlind,
                multiwayBigBlind,
                GetNextSeat(multiwayBigBlind, participatingPlayers),
                smallBlind);
        }

        private static void ValidateParticipants(IReadOnlyList<bool> participatingPlayers)
        {
            if (participatingPlayers == null)
                throw new ArgumentNullException(nameof(participatingPlayers));
            if (participatingPlayers.Count == 0)
                throw new ArgumentException("At least one seat is required.", nameof(participatingPlayers));
        }
    }
}
