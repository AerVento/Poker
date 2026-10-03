namespace Game.Network.Accounts
{
    public enum LeaderboardKind : byte { All, Wealth, Service }

    public struct LeaderboardEntry
    {
        public long Rank;
        public string UserName;
        public string NamecardId;
        public ulong ServiceYears;
        // Historical field name; wealth rankings expose dollars + chips without currency separation.
        public ulong Chips;
        public bool IsSelf;
    }

    // Only public contract totals are projected; unrelated inventory and account secrets stay on the server.
    public struct ServiceLeaderboardEntry
    {
        public long Rank;
        public string UserName;
        /// <summary>Held quantity of each contract, in Contracts.json order.</summary>
        public uint[] Contracts;
        public ulong ServiceYears;
        public bool IsSelf;
    }

    public struct LeaderboardSnapshot
    {
        public LeaderboardEntry[] TopPlayers;
        public LeaderboardEntry Self;
        public ServiceLeaderboardEntry[] ServiceTopPlayers;
        public ServiceLeaderboardEntry ServiceSelf;
    }

    public interface ILeaderboardRepository
    {
        LeaderboardSnapshot ReadLeaderboard(string accountId, LeaderboardKind kind = LeaderboardKind.All);
    }
}
