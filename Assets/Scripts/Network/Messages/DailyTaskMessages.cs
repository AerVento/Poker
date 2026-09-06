using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public struct DailyTasksRequest : NetworkMessage
    {
        public ulong RequestId;
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
    }

    public struct DailyTaskClaimResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public DailyTaskId TaskId;
        public DailyTaskResult Result;
        public uint GrantedAmount;
    }
}
