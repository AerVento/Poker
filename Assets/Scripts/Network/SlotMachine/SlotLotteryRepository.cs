using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace Game.Network.Accounts
{
    public enum SlotLotteryResult : byte
    {
        Success, Unavailable, InvalidRequest, InsufficientFunds, ConfigurationChanged, RewardLimit, RateLimited
    }

    public sealed class SlotDrawOutcome
    {
        public SlotPrizeTier Tier { get; set; }
        public Reward Reward { get; set; }
    }

    public sealed class SlotDrawReceipt
    {
        public string OperationId { get; set; }
        public uint CostDollars { get; set; }
        public SlotPrizeTier Tier { get; set; }
        public Reward Reward { get; set; }
        public SlotDrawOutcome[] Results { get; set; } = Array.Empty<SlotDrawOutcome>();
        [JsonIgnore] public int DrawCount => Results.Length == 0 ? 1 : Results.Length;
        [JsonIgnore] public int WinCount => Results.Length == 0 ? (Reward == null ? 0 : 1) : Results.Count(r => r.Reward != null);

        public RewardEntry[] RewardItems()
        {
            var rewards = Results.Length == 0 ? new[] { Reward } : Results.Select(r => r.Reward);
            return rewards.Where(r => r != null).SelectMany(r => r.Entries).ToArray();
        }
    }

    public sealed class SlotDrawExecution
    {
        public SlotLotteryResult Result { get; set; }
        public SlotDrawReceipt Receipt { get; set; }
        public bool NewlyApplied { get; set; }
    }

    public interface ISlotLotteryRepository
    {
        SlotDrawExecution DrawSlot(string accountId, string operationId, string revision, SlotLotteryCatalog catalog, GameCatalog items, int count = 1);
        SlotDrawReceipt ReadPendingSlot(string accountId);
        void AcknowledgeSlot(string accountId, string operationId);
        /// <summary>Consecutive draws without a prize since the last win, used by the pity guarantee.</summary>
        int ReadSlotMisses(string accountId);
    }
}
