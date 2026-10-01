namespace Game.Network.Accounts
{
    // Server-created authorization for one account, room and hand; never read from a client packet.
    public sealed class TaskRewardAccess
    {
        public string AccountId { get; }
        public int RoomId { get; }
        public string HandId { get; }

        public TaskRewardAccess(string accountId, int roomId, string handId = null)
        {
            AccountId = accountId;
            RoomId = roomId;
            HandId = handId;
        }
    }
}
