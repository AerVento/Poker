using System;
using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        /// <summary>The item catalog of the running server; null while it is stopped.</summary>
        public GameCatalog ItemCatalog => _itemRules?.Catalog;

        // Trusted server code only; request IDs are persisted for repeat-safe payouts.
        public ChipMutationResult GrantDollars(string accountId, string operationId, uint amount)
        {
            if (!NetworkServer.active || _serverStorageFailed || WalletRepository == null ||
                !Guid.TryParseExact(operationId, "N", out _) || amount == 0)
                return ChipMutationResult.AccountNotFound;
            var result = WalletRepository.ApplyDollarMutation(accountId, "grant-dollars:" + accountId + ":" + operationId,
                "ServerDollarGrant", amount, null, out var account);
            if (account != null && (result == ChipMutationResult.Applied || result == ChipMutationResult.AlreadyApplied))
            {
                var connection = _playerManager.GetConnection(account.UserName);
                if (connection != null && connection.isAuthenticated)
                {
                    if (_playerManager.TryGetSession(connection, out var session)) session.Account = account.Copy();
                    connection.Send(new AccountBalanceSync { Dollars = account.Dollars, Chips = account.Chips });
                }
            }
            return result;
        }

        // Trusted server code on Unity's main thread only. No Mirror message exposes this operation to clients.
        public ItemGrantExecution GrantItem(string accountId, string itemId, string operationId, uint quantity = 1)
        {
            if (!NetworkServer.active || _serverStorageFailed || ItemCatalog == null)
                return new ItemGrantExecution(ItemGrantResult.Unavailable);
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as IItemGrantRepository;
            if (repository == null)
                return new ItemGrantExecution(ItemGrantResult.Unavailable);
            var execution = repository.GrantItem(accountId, itemId, operationId, quantity, ItemCatalog);
            if (execution.IsNewlyApplied)
            {
                var account = (authenticator as AccountAuthenticator)?.AccountRepository.FindById(accountId);
                var connection = account == null ? null : _playerManager.GetConnection(account.UserName);
                if (connection != null && connection.isAuthenticated) connection.Send(ReadCollection(connection));
            }
            return execution;
        }
    }
}
