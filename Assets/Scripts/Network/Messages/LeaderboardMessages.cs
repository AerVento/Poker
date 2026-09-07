using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public enum LeaderboardResult : byte
    {
        Success,
        Unavailable,
        InvalidRequest,
        RateLimited,
    }

    public struct LeaderboardRequest : NetworkMessage
    {
        public ulong RequestId;
    }

    public struct LeaderboardResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public LeaderboardResult Result;
        public LeaderboardSnapshot Snapshot;
    }
}
