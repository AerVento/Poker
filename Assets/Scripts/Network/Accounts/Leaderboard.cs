namespace Game.Network.Accounts
{
    public struct LeaderboardEntry
    {
        public long Rank;
        public string UserName;
        public string NamecardId;
        public ulong ServiceYears;
        public uint Chips;
        public bool IsSelf;
    }

    // Only public contract totals are projected; unrelated inventory and account secrets stay on the server.
    public struct ServiceLeaderboardEntry
    {
        public long Rank;
        public string UserName;
        public uint DevilContracts;
        public uint ArchdevilContracts;
        public uint SatanContracts;
        public bool IsSelf;

        public ulong ServiceYears => InfernalNamecard.CalculateYears(DevilContracts, ArchdevilContracts, SatanContracts);
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
        LeaderboardSnapshot ReadLeaderboard(string accountId);
    }
}
