using System;
using System.Linq;

namespace Game
{
    public sealed class SlotLotteryDisclosure
    {
        public const int PageSize = 6;
        public int NoneWeight { get; set; }
        public int SmallWeight { get; set; }
        public int MediumWeight { get; set; }
        public int GrandWeight { get; set; }
        public SlotPrizeTier Tier { get; set; }
        public int PoolWeight { get; set; }
        public int Offset { get; set; }
        public int TotalRewards { get; set; }
        public SlotRewardEntry[] Rewards { get; set; } = Array.Empty<SlotRewardEntry>();
        public int PityDraws { get; set; }
        /// <summary>This player's consecutive misses since the last win.</summary>
        public int PityMisses { get; set; }
        /// <summary>Draws until a prize is certain (1 = the next draw); 0 when there is no guarantee.</summary>
        public int DrawsUntilGuaranteed => PityDraws > 0 ? Math.Max(1, PityDraws - PityMisses) : 0;
        public long TotalWeight => (long)NoneWeight + SmallWeight + MediumWeight + GrandWeight;
        public double Probability(SlotPrizeTier tier)
        {
            int weight = tier == SlotPrizeTier.Small ? SmallWeight : tier == SlotPrizeTier.Medium ? MediumWeight :
                tier == SlotPrizeTier.Grand ? GrandWeight : NoneWeight;
            return TotalWeight > 0 ? weight / (double)TotalWeight : 0;
        }
        public double PoolProbability(SlotRewardEntry entry) => PoolWeight > 0 ? entry.Weight / (double)PoolWeight : 0;
        /// <summary>A disclosed reward with weight 0 is one this player already owns and cannot draw.</summary>
        public static bool IsOwned(SlotRewardEntry entry) => entry.Weight == 0;

        /// <summary>
        /// Odds as the draw applies them to this player: rewards that no longer fit are shown with weight 0,
        /// and a tier with nothing left moves its weight to no prize.
        /// </summary>
        public static SlotLotteryDisclosure From(SlotLotteryCatalog catalog, SlotPrizeTier tier = SlotPrizeTier.None, int offset = 0,
            Func<Reward, bool> eligible = null)
        {
            if (catalog == null) return null;
            int Live(SlotPrizeTier t) => eligible == null || catalog.EligibleWeight(t, eligible) > 0 ? catalog.Pool(t).Weight : 0;
            int small = Live(SlotPrizeTier.Small), medium = Live(SlotPrizeTier.Medium), grand = Live(SlotPrizeTier.Grand);
            var pool = catalog.Pool(tier);
            int count = pool?.Rewards.Length ?? 0;
            int start = count == 0 ? 0 : Math.Min(Math.Max(offset, 0) / PageSize * PageSize, (count - 1) / PageSize * PageSize);
            return new SlotLotteryDisclosure
            {
                NoneWeight = catalog.TotalWeight - small - medium - grand, SmallWeight = small,
                MediumWeight = medium, GrandWeight = grand,
                PityDraws = catalog.PityDraws,
                Tier = tier, PoolWeight = pool == null ? 0 : catalog.EligibleWeight(tier, eligible),
                Offset = start, TotalRewards = count,
                Rewards = pool?.Rewards.Skip(start).Take(PageSize).Select(r => eligible == null || eligible(r.Reward) ? r :
                    new SlotRewardEntry { Weight = 0, Reward = r.Reward }).ToArray() ?? Array.Empty<SlotRewardEntry>()
            };
        }
    }
}
