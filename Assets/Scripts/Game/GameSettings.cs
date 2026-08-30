namespace Game
{
    public class GameSettings
    {
        public string AccountId { get; private set; }

        public string UserName { get; private set; }

        public string Nickname => UserName;

        public uint Chips { get; private set; }

        public int? LastRoomId { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccountId);

        public void SetAuthenticatedAccount(
            string accountId,
            string userName,
            uint chips,
            int? lastRoomId)
        {
            AccountId = accountId;
            UserName = userName;
            Chips = chips;
            LastRoomId = lastRoomId;
        }

        public void SetLastRoom(int? roomId)
        {
            LastRoomId = roomId;
        }
    }
}
