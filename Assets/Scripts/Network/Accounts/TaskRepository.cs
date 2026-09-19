using System;
using System.Collections.Generic;
using Game.Facts;
using Game.Tasks;

namespace Game.Network.Accounts
{
    public sealed class PlayerTaskInstance
    {
        public string InstanceId { get; set; }
        public string DefinitionId { get; set; }
        public string DefinitionJson { get; set; }
        public string Revision { get; set; }
        public bool IsRandom { get; set; }
        public int? SlotId { get; set; }
        public bool RefreshUsed { get; set; }
        public bool OutDated { get; set; }
        public int DisplayOrder { get; set; }
        public int Period { get; set; }
        public long AssignedAt { get; set; }
        public long ExpiresAt { get; set; }
        public TaskState State { get; set; }
        public Dictionary<string, uint> ItemLimits { get; set; } = new Dictionary<string, uint>();
    }

    public sealed class TaskRewardItem
    {
        public string ItemId { get; set; }
        public uint Count { get; set; }
    }

    public sealed class TaskClaimReceipt
    {
        public string InstanceId { get; set; }
        public int Ordinal { get; set; }
        public uint Chip { get; set; }
        public TaskRewardItem[] Items { get; set; }
    }

    public sealed class PlayerTaskDocument
    {
        public long Revision { get; set; }
        public int FixedPeriod { get; set; }
        public int RandomPeriod { get; set; }
        public List<PlayerTaskInstance> Instances { get; set; } = new List<PlayerTaskInstance>();
        public List<TaskClaimReceipt> Receipts { get; set; } = new List<TaskClaimReceipt>();
        public List<string> DeletedInstanceIds { get; set; } = new List<string>();
    }

    public abstract class TaskAccountTransaction
    {
        public PlayerAccount Account { get; protected set; }
        public bool HasActiveHand { get; protected set; }
        public PlayerTaskDocument Document { get; protected set; }
        public bool AlreadyConsumed { get; protected set; }
        public bool Changed { get; set; }
        public virtual DailyTaskResult RewardBlockReason => DailyTaskResult.Success;
        public virtual void AuthorizeRewardAccess(TaskRewardAccess access) { }
        public abstract bool HasLegacyClaim(string key);
        public abstract DailyTaskResult Grant(Reward reward, IReadOnlyDictionary<string, uint> itemLimits, string claimKey);
    }

    public interface ITaskRepository : IHandFactPublisher
    {
        void InitializeTasks();
        string[] GetTaskAccountIds();
        T TransactTasks<T>(string accountId, Func<TaskAccountTransaction, T> operation, string eventId = null);
        HandFact[] ReadPendingHandFacts(int limit);
    }
}
