using System;
using System.Collections.Generic;
using Game.Facts;
using Game.Tasks;

namespace Game.Network.Accounts
{
    public sealed class PlayerTaskInstance
    {
        public string InstanceId { get; set; }
        /// <summary>The task ID in TaskDefinitions.json; name, rule, reward and order come from the current configuration.</summary>
        public string DefinitionId { get; set; }
        public bool IsRandom { get; set; }
        public int? SlotId { get; set; }
        public bool RefreshUsed { get; set; }
        public bool OutDated { get; set; }
        public int Period { get; set; }
        public long AssignedAt { get; set; }
        public long ExpiresAt { get; set; }
        public TaskState State { get; set; }
    }

    public sealed class TaskClaimReceipt
    {
        public string InstanceId { get; set; }
        public int Ordinal { get; set; }
        public Reward Reward { get; set; } = Reward.Empty;
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
        public Reward GrantedReward { get; protected set; } = Reward.Empty;
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
