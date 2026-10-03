namespace Game.Network.Accounts
{
    public sealed partial class GuardedAccountRepository : IWalletRepository
    {
        public WalletSnapshot ReadWallet(string accountId) => Execute(() => ((IWalletRepository)_inner).ReadWallet(accountId));
        public WalletResult ExchangeWallet(string accountId, string operationId, bool dollarsToChips, uint amount)
            => Execute(() => ((IWalletRepository)_inner).ExchangeWallet(accountId, operationId, dollarsToChips, amount));
        public void SetHandWalletAccess(string handId, string accountId, uint committedChips, bool canExchange)
            => Execute(() => { ((IWalletRepository)_inner).SetHandWalletAccess(handId, accountId, committedChips, canExchange); return true; });
        public ChipMutationResult ApplyDollarMutation(string accountId, string requestId, string transactionType,
            long amount, uint? maximumBalanceInclusive, out PlayerAccount account)
        {
            PlayerAccount snapshot = null;
            var result = Execute(() => ((IWalletRepository)_inner).ApplyDollarMutation(accountId, requestId,
                transactionType, amount, maximumBalanceInclusive, out snapshot));
            account = snapshot;
            return result;
        }
    }
}
