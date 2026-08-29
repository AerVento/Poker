namespace Game
{
    public class GameSettings
    {
        public string AccountId { get; private set; }

        public string UserName { get; private set; }

        public string Nickname => UserName;

        public long Chips { get; private set; }

        public int? LastRoomId { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccountId);

        public void SetAuthenticatedAccount(
            string accountId,
            string userName,
            long chips,
            int? lastRoomId)
        {
            AccountId = accountId;
            UserName = userName;
            Chips = chips;
            LastRoomId = lastRoomId;
        }
    }
}
