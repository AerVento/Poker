using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    /// <summary>
    /// 根据玩家在整手牌中的总投入计算主池、边池和未被跟注的退款。
    /// 规则依据：Poker TDA Rule 21。使用整手牌总投入可以确保短筹码玩家
    /// 不会参与其 all-in 之后其他玩家在后续街投入的筹码。
    /// </summary>
    public static class PotCalculator
    {
        public sealed class Pot
        {
            public uint ChipCount { get; }

            public List<int> EligiblePlayers { get; }

            public Pot(uint chipCount, List<int> eligiblePlayers)
            {
                ChipCount = chipCount;
                EligiblePlayers = eligiblePlayers;
            }
        }

        public sealed class Result
        {
            public List<Pot> Pots { get; }

            public uint[] Refunds { get; }

            public Result(List<Pot> pots, uint[] refunds)
            {
                Pots = pots;
                Refunds = refunds;
            }
        }

        public static Result Calculate(IReadOnlyList<uint> contributions, IReadOnlyList<bool> foldedPlayers)
        {
            if (contributions == null)
                throw new ArgumentNullException(nameof(contributions));
            if (foldedPlayers == null)
                throw new ArgumentNullException(nameof(foldedPlayers));
            if (contributions.Count != foldedPlayers.Count)
                throw new ArgumentException("Contributions and folded player lists must have the same length.");

            var adjustedContributions = contributions.ToArray();
            var refunds = new uint[contributions.Count];
            RefundUncalledChips(adjustedContributions, refunds);

            var levels = adjustedContributions
                .Where(contribution => contribution > 0)
                .Distinct()
                .OrderBy(contribution => contribution)
                .ToArray();

            var pots = new List<Pot>(levels.Length);
            uint previousLevel = 0;
            foreach (var level in levels)
            {
                var contributorCount = 0;
                var eligiblePlayers = new List<int>();
                for (int playerIndex = 0; playerIndex < adjustedContributions.Length; playerIndex++)
                {
                    if (adjustedContributions[playerIndex] < level)
                        continue;

                    contributorCount++;
                    if (!foldedPlayers[playerIndex])
                        eligiblePlayers.Add(playerIndex);
                }

                if (eligiblePlayers.Count == 0)
                    throw new InvalidOperationException("A pot layer has no eligible player.");

                var layerChipCount = (ulong)(level - previousLevel) * (uint)contributorCount;
                if (layerChipCount > uint.MaxValue)
                    throw new OverflowException("The chip count of a pot exceeds UInt32.MaxValue.");

                pots.Add(new Pot((uint)layerChipCount, eligiblePlayers));
                previousLevel = level;
            }

            ValidateChipConservation(contributions, pots, refunds);
            return new Result(pots, refunds);
        }

        public static List<int> GetOddChipOrder(IReadOnlyCollection<int> playerIndices, int dealer, int playerCount)
        {
            if (playerIndices == null)
                throw new ArgumentNullException(nameof(playerIndices));
            if (playerCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            if (dealer < 0 || dealer >= playerCount)
                throw new ArgumentOutOfRangeException(nameof(dealer));
            if (playerIndices.Count == 0)
                throw new ArgumentException("At least one player is required.", nameof(playerIndices));
            if (playerIndices.Any(index => index < 0 || index >= playerCount))
                throw new ArgumentOutOfRangeException(nameof(playerIndices));
            if (playerIndices.Distinct().Count() != playerIndices.Count)
                throw new ArgumentException("Player indices must be unique.", nameof(playerIndices));

            return playerIndices
                .OrderBy(index => (index - dealer - 1 + playerCount) % playerCount)
                .ToList();
        }

        private static void RefundUncalledChips(uint[] contributions, uint[] refunds)
        {
            uint highest = 0;
            uint secondHighest = 0;
            var highestPlayer = -1;
            var highestCount = 0;

            for (int playerIndex = 0; playerIndex < contributions.Length; playerIndex++)
            {
                var contribution = contributions[playerIndex];
                if (contribution > highest)
                {
                    secondHighest = highest;
                    highest = contribution;
                    highestPlayer = playerIndex;
                    highestCount = 1;
                }
                else if (contribution == highest)
                {
                    highestCount++;
                }
                else if (contribution > secondHighest)
                {
                    secondHighest = contribution;
                }
            }

            if (highestPlayer < 0 || highestCount != 1 || highest == secondHighest)
                return;

            refunds[highestPlayer] = highest - secondHighest;
            contributions[highestPlayer] = secondHighest;
        }

        private static void ValidateChipConservation(
            IReadOnlyList<uint> contributions,
            IReadOnlyList<Pot> pots,
            IReadOnlyList<uint> refunds)
        {
            ulong contributedChipCount = 0;
            foreach (var contribution in contributions)
                contributedChipCount += contribution;

            ulong distributedChipCount = 0;
            foreach (var pot in pots)
                distributedChipCount += pot.ChipCount;
            foreach (var refund in refunds)
                distributedChipCount += refund;

            if (contributedChipCount != distributedChipCount)
                throw new InvalidOperationException("Pot calculation did not conserve chips.");
        }
    }
}
