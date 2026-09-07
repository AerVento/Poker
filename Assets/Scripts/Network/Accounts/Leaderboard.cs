namespace Game.Network.Accounts
{
    public struct LeaderboardEntry
    {
        public long Rank;
        public string UserName;
        public string NamecardId;
        public uint Chips;
        public bool IsSelf;
    }

    public struct LeaderboardSnapshot
    {
        public LeaderboardEntry[] TopPlayers;
        public LeaderboardEntry Self;
    }

    public interface ILeaderboardRepository
    {
        LeaderboardSnapshot ReadLeaderboard(string accountId);
    }
}
