namespace Game.Network.Accounts
{
    public enum WalletResult : byte
    {
        Success, AlreadyApplied, InvalidRequest, Unavailable, ExchangeLocked,
        InsufficientFunds, BalanceLimit, ConversionRequired, QuoteChanged, RateLimited
    }

    public sealed class WalletSnapshot
    {
        public PlayerAccount Account { get; set; }
        public uint AvailableChips { get; set; }
        public bool CanExchange { get; set; }
    }

    // Only server-owned game state may update a participant's exchange access.
    public interface IWalletRepository
    {
        WalletSnapshot ReadWallet(string accountId);
        WalletResult ExchangeWallet(string accountId, string operationId, bool dollarsToChips, uint amount);
        void SetHandWalletAccess(string handId, string accountId, uint committedChips, bool canExchange);
        ChipMutationResult ApplyDollarMutation(string accountId, string requestId, string transactionType,
            long amount, uint? maximumBalanceInclusive, out PlayerAccount account);
    }
}
