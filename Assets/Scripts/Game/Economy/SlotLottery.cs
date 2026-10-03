using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    public enum SlotPrizeTier : byte { None, Small, Medium, Grand }
    public sealed class SlotRewardEntry
    {
        public int Weight { get; set; }
        public Reward Reward { get; set; }
    }

    public sealed class SlotRewardPool
    {
        public int Weight { get; set; }
        public SlotRewardEntry[] Rewards { get; set; } = Array.Empty<SlotRewardEntry>();
    }

    public sealed class SlotLotteryCatalog
    {
        public bool Enabled { get; set; } = true;
        public uint CostDollars { get; set; }
        public int NoneWeight { get; set; }
        /// <summary>After PityDraws - 1 consecutive misses the next draw always wins; 0 disables the guarantee.</summary>
        public int PityDraws { get; set; }
        public SlotRewardPool Small { get; set; }
        public SlotRewardPool Medium { get; set; }
        public SlotRewardPool Grand { get; set; }
        [System.Text.Json.Serialization.JsonIgnore] public string Revision { get; set; }
        public SlotRewardPool Pool(SlotPrizeTier tier) => tier == SlotPrizeTier.Small ? Small : tier == SlotPrizeTier.Medium ? Medium : tier == SlotPrizeTier.Grand ? Grand : null;
        public int TotalWeight => checked(NoneWeight + Small.Weight + Medium.Weight + Grand.Weight);

        public void Validate(Func<string, uint?> itemLimit)
        {
            if (CostDollars == 0 || Small == null || Medium == null || Grand == null || NoneWeight < 0)
                throw new InvalidOperationException("SlotMachine requires a positive cost and all three pools.");
            if (PityDraws != 0 && (PityDraws < 2 || PityDraws > 100))
                throw new InvalidOperationException("Slot pity draws must be 0 or between 2 and 100.");
            foreach (var pool in new[] { Small, Medium, Grand })
            {
                if (pool.Weight < 0 || pool.Rewards == null || pool.Rewards.Length > 200 ||
                    pool.Weight > 0 && pool.Rewards.Length == 0)
                    throw new InvalidOperationException("Invalid slot pool weight or reward count.");
                long sum = 0;
                foreach (var entry in pool.Rewards)
                {
                    if (entry == null ||
                        entry.Weight <= 0 || entry.Reward == null || entry.Reward.Entries.Count == 0 || entry.Reward.Entries.Count > 8)
                        throw new InvalidOperationException("Invalid slot reward entry.");
                    entry.Reward.Validate(itemLimit);
                    sum += entry.Weight;
                }
                if (sum > int.MaxValue) throw new InvalidOperationException("Slot reward weights exceed the supported total.");
            }
            if ((long)NoneWeight + Small.Weight + Medium.Weight + Grand.Weight > int.MaxValue || TotalWeight <= 0)
                throw new InvalidOperationException("Invalid slot tier weight total.");
        }

        public SlotPrizeTier SelectTier(int roll)
        {
            if (roll < 0 || roll >= TotalWeight) throw new ArgumentOutOfRangeException(nameof(roll));
            if ((roll -= NoneWeight) < 0) return SlotPrizeTier.None;
            if ((roll -= Small.Weight) < 0) return SlotPrizeTier.Small;
            if ((roll -= Medium.Weight) < 0) return SlotPrizeTier.Medium;
            return SlotPrizeTier.Grand;
        }

        /// <summary>Whether the next draw is guaranteed to win after this many consecutive misses.</summary>
        public bool IsGuaranteed(int misses) => PityDraws > 0 && misses >= PityDraws - 1;

        /// <summary>Combined weight of the prize tiers that still have something to give.</summary>
        public int WinningWeight(Func<Reward, bool> eligible = null) =>
            new[] { SlotPrizeTier.Small, SlotPrizeTier.Medium, SlotPrizeTier.Grand }
                .Where(t => EligibleWeight(t, eligible) > 0).Sum(t => Pool(t).Weight);

        /// <summary>A guaranteed draw skips no prize and keeps the prize tiers' relative weights.</summary>
        public SlotPrizeTier SelectWinningTier(int roll, Func<Reward, bool> eligible = null)
        {
            if (roll < 0 || roll >= WinningWeight(eligible)) throw new ArgumentOutOfRangeException(nameof(roll));
            foreach (var tier in new[] { SlotPrizeTier.Small, SlotPrizeTier.Medium, SlotPrizeTier.Grand })
                if (EligibleWeight(tier, eligible) > 0 && (roll -= Pool(tier).Weight) < 0) return tier;
            throw new InvalidOperationException("Invalid weighted roll.");
        }

        /// <summary>Total weight of the rewards in a tier that may still be drawn; 0 means the tier yields no prize.</summary>
        public int EligibleWeight(SlotPrizeTier tier, Func<Reward, bool> eligible = null) =>
            Pool(tier)?.Rewards.Where(r => eligible == null || eligible(r.Reward)).Sum(r => r.Weight) ?? 0;

        /// <summary>Picks among eligible rewards only; <paramref name="roll"/> is below <see cref="EligibleWeight"/>.</summary>
        public SlotRewardEntry SelectReward(SlotPrizeTier tier, int roll, Func<Reward, bool> eligible = null)
        {
            var pool = Pool(tier) ?? throw new ArgumentOutOfRangeException(nameof(tier));
            if (roll < 0 || roll >= EligibleWeight(tier, eligible)) throw new ArgumentOutOfRangeException(nameof(roll));
            foreach (var reward in pool.Rewards)
                if ((eligible == null || eligible(reward.Reward)) && (roll -= reward.Weight) < 0) return reward;
            throw new InvalidOperationException("Invalid weighted roll.");
        }

        /// <summary>
        /// A reward is drawable only while every item in it still fits under the holding limit, so an owned
        /// namecard or card face never wastes a draw. Currency is not checked here.
        /// </summary>
        public static bool Fits(Reward reward, IReadOnlyDictionary<string, uint> owned, IReadOnlyDictionary<string, uint> limits)
        {
            foreach (var group in reward.Entries.Where(e => e.Kind == RewardItemKind.Item).GroupBy(e => e.ItemId))
            {
                if (!limits.TryGetValue(group.Key, out var maximum)) continue;
                owned.TryGetValue(group.Key, out var current);
                ulong quantity = 0;
                foreach (var entry in group) quantity += entry.Quantity;
                if (current >= maximum || quantity > maximum - current) return false;
            }
            return true;
        }
    }
}
