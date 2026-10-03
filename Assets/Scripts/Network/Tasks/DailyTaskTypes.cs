using System;

namespace Game.Network.Accounts
{
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

    public struct DailyTaskState
    {
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
        public string Name;
        public string Description;
        public Reward Reward;
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

    // Every task day starts at 00:00 UTC+8.
    public static class TaskPeriod
    {
        public static readonly TimeSpan ResetOffset = TimeSpan.FromHours(8);

        internal static int GetPeriod(DateTimeOffset now)
        {
            var day = now.ToOffset(ResetOffset);
            return day.Year * 10000 + day.Month * 100 + day.Day;
        }
    }
}
