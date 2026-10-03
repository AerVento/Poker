using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public struct DailyTaskDeleteRequest : NetworkMessage
    {
        public ulong RequestId;
        public string InstanceId;
        public bool Refresh;
    }

    public struct DailyTaskDeleteResponse : NetworkMessage
    {
        public bool Refreshed;
        public ulong RequestId;
        public string AccountId;
        public string InstanceId;
        public DailyTaskResult Result;
        public Reward GrantedReward;
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
        public string InstanceId;
        public int CompletionOrdinal;
    }

    public struct DailyTaskClaimResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public DailyTaskResult Result;
        public Reward GrantedReward;
        public string InstanceId;
        public int CompletionOrdinal;
    }
}
