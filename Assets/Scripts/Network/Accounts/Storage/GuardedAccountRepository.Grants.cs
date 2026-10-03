namespace Game.Network.Accounts
{
    public sealed partial class GuardedAccountRepository : IItemGrantRepository
    {
        public ItemGrantExecution GrantItem(string accountId, string itemId, string operationId,
            uint quantity, GameCatalog catalog)
        {
            return Execute(() => ((IItemGrantRepository)_inner).GrantItem(accountId, itemId, operationId, quantity, catalog));
        }
    }
}
