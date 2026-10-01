namespace Game.Network.Accounts
{
    // Values are persisted in grant receipts. Append new results without renumbering existing ones.
    public enum ItemGrantResult
    {
        Success, AlreadyOwned, UnknownItem, AccountNotFound, QuantityLimit, InvalidRequest, Unavailable
    }

    public sealed class ItemGrantExecution
    {
        public ItemGrantResult Result { get; }
        public bool IsNewlyApplied { get; }

        public ItemGrantExecution(ItemGrantResult result, bool isNewlyApplied = false)
        {
            Result = result;
            IsNewlyApplied = isNewlyApplied;
        }
    }

    public interface IItemGrantRepository
    {
        // Trusted server callers only. Reuse operationId when retrying the same grant.
        ItemGrantExecution GrantItem(string accountId, string itemId, string operationId,
            uint quantity, ItemIdentityRegistry registry);
    }
}
