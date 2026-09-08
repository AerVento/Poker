using System;
using System.Linq;
using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        public ItemIdentityRegistry ItemIdentities { get; private set; }

        private void InitializeItemGrants()
        {
            if (_shopCatalog == null || _itemRuntime == null)
                throw new InvalidOperationException("Load both shop catalogs before initializing item grants.");
            ItemIdentities = new ItemIdentityRegistry(_shopCatalog, new ItemServerCatalog
            {
                Products = _itemRuntime.Products.Select(product => new ServerItemDefinition
                {
                    Id = product.Id, Kind = product.Kind, MaxQuantity = product.MaxQuantity,
                    Price = product.Price, Listed = product.Listed
                }).ToArray()
            });
        }

        private void ClearItemGrants()
        {
            ItemIdentities = null;
        }

        // Trusted server code on Unity's main thread only. No Mirror message exposes this operation to clients.
        public ItemGrantExecution GrantItem(string accountId, string itemId, string operationId, uint quantity = 1)
        {
            if (!NetworkServer.active || _serverStorageFailed || ItemIdentities == null)
                return new ItemGrantExecution(ItemGrantResult.Unavailable);
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as IItemGrantRepository;
            if (repository == null)
                return new ItemGrantExecution(ItemGrantResult.Unavailable);
            return repository.GrantItem(accountId, itemId, operationId, quantity, ItemIdentities);
        }
    }
}
