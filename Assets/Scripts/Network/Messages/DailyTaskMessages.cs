using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public struct DailyTaskDeleteRequest : NetworkMessage
    {
        public ulong RequestId;
        public string InstanceId;
    }

    public struct DailyTaskDeleteResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public string InstanceId;
        public DailyTaskResult Result;
        public uint GrantedAmount;
        public TaskRewardItemState[] GrantedItems;
    }

    public struct DailyTasksRequest : NetworkMessage
    {
        public ulong RequestId;
        public int Offset;
        public long Revision;
    }

    public struct DailyTasksResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public DailyTaskResult Result;
        public DailyTasksSnapshot Snapshot;
    }

    public struct DailyTaskClaimRequest : NetworkMessage
    {
        public ulong RequestId;
        public DailyTaskId TaskId;
        public int Period;
        public string InstanceId;
        public int CompletionOrdinal;
    }

    public struct DailyTaskClaimResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public DailyTaskId TaskId;
        public DailyTaskResult Result;
        public uint GrantedAmount;
        public string InstanceId;
        public int CompletionOrdinal;
        public TaskRewardItemState[] GrantedItems;
    }
}
