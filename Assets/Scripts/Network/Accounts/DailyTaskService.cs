using System;
using System.Globalization;

namespace Game.Network.Accounts
{
    public enum DailyTaskId : byte
    {
        DailySupply = 1,
        Comeback = 2,
    }

    public enum DailyTaskAvailability : byte
    {
        Claimable,
        Claimed,
        BalanceTooHigh,
        ActiveHand,
        BalanceLimit,
        InProgress, Expired, ItemLimit, OutsideRoom,
    }

    public enum DailyTaskResult : byte
    {
        Success,
        AlreadyClaimed,
        BalanceTooHigh,
        ActiveHand,
        OutsideRoomRequired,
        PeriodChanged,
        InvalidRequest,
        BalanceLimit,
        Unavailable,
        NotComplete, Expired, ItemLimit, SnapshotChanged,
        AlreadyDeleted, FixedTask, RefreshUnavailable, NoAlternative,
    }

    public struct TaskObjectiveState
    {
        public string Description;
        public int Progress;
        public int Target;
    }

    public struct TaskRewardItemState
    {
        public string ItemId;
        public uint Count;
    }

    public struct DailyTaskState
    {
        public DailyTaskId Id;
        public string InstanceId;
        public string DefinitionId;
        public bool IsRandom;
        public bool CanDelete;
        public bool CanRefresh;
        public bool OutDated;
        public int SlotId;
        public bool IsDirectClaim;
        public int CompletedCount;
        public int ClaimedCount;
        public int MaxCompletions;
        public int ClaimOrdinal;
        public long ExpiresAtUnixSeconds;
        public TaskObjectiveState[] Objectives;
        public TaskRewardItemState[] RewardItems;
        public string Name;
        public string Description;
        public uint Reward;
        public DailyTaskAvailability Availability;
    }

    public struct DailyTasksSnapshot
    {
        public int Period;
        public long Revision;
        public int Offset;
        public int NextOffset;
        public int TotalCount;
        public long ServerUnixSeconds;
        public long NextResetUnixSeconds;
        public uint Balance;
        public DailyTaskState[] Tasks;
    }

    public sealed class DailyRewardAccountState
    {
        public PlayerAccount Account { get; set; }
        public bool HasActiveHand { get; set; }
        public bool SupplyClaimed { get; set; }
        public bool ComebackClaimed { get; set; }
    }

    public interface IDailyRewardRepository
    {
        DailyRewardAccountState ReadDailyRewards(string accountId, string supplyKey, string comebackKey);
        ChipMutationResult ClaimDailyReward(string accountId, string claimKey, uint amount,
            uint? maximumBalanceInclusive, out PlayerAccount account);
    }

    public sealed class DailyTaskService
    {
        public const uint SupplyReward = 1000;
        public const uint ComebackThreshold = 1000;
        public const uint ComebackReward = 2000;
        public static readonly TimeSpan ResetOffset = TimeSpan.FromHours(8);

        private readonly IDailyRewardRepository _repository;
        private readonly Func<DateTimeOffset> _utcNow;

        public DailyTaskService(IDailyRewardRepository repository, Func<DateTimeOffset> utcNow = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public DailyTasksSnapshot GetStatus(string accountId)
        {
            var now = _utcNow();
            var day = now.ToOffset(ResetOffset);
            var period = GetPeriod(now);
            var state = _repository.ReadDailyRewards(accountId,
                ClaimKey(accountId, DailyTaskId.DailySupply, period),
                ClaimKey(accountId, DailyTaskId.Comeback, period));
            if (state.Account == null)
                throw new InvalidOperationException("The authenticated account no longer exists.");

            return new DailyTasksSnapshot
            {
                Period = period,
                ServerUnixSeconds = now.ToUnixTimeSeconds(),
                NextResetUnixSeconds = new DateTimeOffset(day.Date.AddDays(1), ResetOffset).ToUnixTimeSeconds(),
                Balance = state.Account.Chips,
                Tasks = new[]
                {
                    CreateState(DailyTaskId.DailySupply, "每日补给", "每日领取一份筹码补给，开启今日牌局。",
                        SupplyReward, state, state.SupplyClaimed, false),
                    CreateState(DailyTaskId.Comeback, "东山再起", $"余额不高于 ${ComebackThreshold:N0} 时，可领取应急筹码。",
                        ComebackReward, state, state.ComebackClaimed, state.Account.Chips > ComebackThreshold),
                },
            };
        }

        public DailyTaskResult Claim(string accountId, DailyTaskId taskId, int requestedPeriod, out PlayerAccount account)
        {
            account = null;
            if (taskId != DailyTaskId.DailySupply && taskId != DailyTaskId.Comeback)
                return DailyTaskResult.InvalidRequest;
            var period = GetPeriod(_utcNow());
            // A packet sent before midnight must not consume the following day's allowance.
            if (requestedPeriod != period)
                return DailyTaskResult.PeriodChanged;

            var result = _repository.ClaimDailyReward(accountId, ClaimKey(accountId, taskId, period),
                taskId == DailyTaskId.DailySupply ? SupplyReward : ComebackReward,
                taskId == DailyTaskId.Comeback ? (uint?)ComebackThreshold : null, out account);
            return result switch
            {
                ChipMutationResult.Applied => DailyTaskResult.Success,
                ChipMutationResult.AlreadyApplied => DailyTaskResult.AlreadyClaimed,
                ChipMutationResult.AccountBusyInHand => DailyTaskResult.ActiveHand,
                ChipMutationResult.BalanceRequirementNotMet => DailyTaskResult.BalanceTooHigh,
                ChipMutationResult.Overflow => DailyTaskResult.BalanceLimit,
                _ => DailyTaskResult.Unavailable,
            };
        }

        internal static int GetPeriod(DateTimeOffset now)
        {
            var day = now.ToOffset(ResetOffset);
            return day.Year * 10000 + day.Month * 100 + day.Day;
        }

        internal static string ClaimKey(string accountId, DailyTaskId taskId, int period)
        {
            return "DailyTask:" + accountId + ":" + ((byte)taskId).ToString(CultureInfo.InvariantCulture) +
                ":" + period.ToString(CultureInfo.InvariantCulture);
        }

        private static DailyTaskState CreateState(DailyTaskId id, string name, string description, uint reward,
            DailyRewardAccountState account, bool claimed, bool balanceTooHigh)
        {
            return new DailyTaskState
            {
                Id = id,
                Name = name,
                Description = description,
                Reward = reward,
                Availability = account.HasActiveHand ? DailyTaskAvailability.ActiveHand :
                    claimed ? DailyTaskAvailability.Claimed :
                    balanceTooHigh ? DailyTaskAvailability.BalanceTooHigh :
                    (ulong)account.Account.Chips + reward > uint.MaxValue ? DailyTaskAvailability.BalanceLimit :
                    DailyTaskAvailability.Claimable,
            };
        }
    }
}
