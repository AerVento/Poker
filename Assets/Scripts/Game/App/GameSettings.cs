namespace Game
{
    public class GameSettings
    {
        public event System.Action WalletChanged;

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
            WalletChanged?.Invoke();
        }

        public void ClearAuthenticatedAccount()
        {
            AccountId = null;
            UserName = null;
            Chips = 0;
            Dollars = 0;
            LastRoomId = null;
            SessionToken = null;
            WalletChanged?.Invoke();
        }

        public void SetLastRoom(int? roomId)
        {
            LastRoomId = roomId;
        }

        public void SetChips(uint chips)
        {
            if (Chips == chips) return;
            Chips = chips;
            WalletChanged?.Invoke();
        }

        public void SetDollars(uint dollars)
        {
            if (Dollars == dollars) return;
            Dollars = dollars;
            WalletChanged?.Invoke();
        }

        public void SetWallet(uint dollars, uint chips)
        {
            if (Dollars == dollars && Chips == chips) return;
            Dollars = dollars;
            Chips = chips;
            WalletChanged?.Invoke();
        }
    }
}
