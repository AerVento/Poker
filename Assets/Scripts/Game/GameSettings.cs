namespace Game
{
    public class GameSettings
    {
        public string AccountId { get; private set; }

        public string UserName { get; private set; }

        public string Nickname => UserName;

        public uint Chips { get; private set; }

        public uint Dollars { get; private set; }

        public ulong TotalAssets => (ulong)Dollars + Chips;

        public int? LastRoomId { get; private set; }

        public string SessionToken { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccountId);

        public void SetAuthenticatedAccount(
            string accountId,
            string userName,
            uint chips,
            int? lastRoomId,
            string sessionToken,
            uint dollars = 0)
        {
            AccountId = accountId;
            UserName = userName;
            Chips = chips;
            Dollars = dollars;
            LastRoomId = lastRoomId;
            SessionToken = sessionToken;
        }

        public void ClearAuthenticatedAccount()
        {
            AccountId = null;
            UserName = null;
            Chips = 0;
            Dollars = 0;
            LastRoomId = null;
            SessionToken = null;
        }

        public void SetLastRoom(int? roomId)
        {
            LastRoomId = roomId;
        }

        public void SetChips(uint chips)
        {
            Chips = chips;
        }

        public void SetDollars(uint dollars)
        {
            Dollars = dollars;
        }

        public void SetWallet(uint dollars, uint chips)
        {
            Dollars = dollars;
            Chips = chips;
        }
    }
}
