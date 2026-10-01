using System.Linq;
using Game.Network.Accounts;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private ShopCatalogMessage? _shopCatalogMessage;

        // Built once per server process from Configs/Shop; every login receives the same public definitions.
        internal ShopCatalogMessage ShopCatalogMessage => _shopCatalogMessage ??= ClientShopCatalog.ToMessage(ShopConfiguration.Server);

        // Products can only be delisted, never removed while players own them; report owned IDs that are gone.
        private void WarnUnconfiguredInventory()
        {
            if (!(authenticator is AccountAuthenticator accounts) || !(accounts.AccountRepository is IStoredProductRepository repository))
                return;
            var definitions = ShopConfiguration.Server;
            var stored = repository.ReadStoredProductIds();
            var missing = stored.Items.Where(id => definitions.FindItem(id) == null).Select(id => "item " + id)
                .Concat(stored.Namecards.Where(id => definitions.FindNamecard(id) == null).Select(id => "namecard " + id))
                .Concat(stored.CardFaces.Where(id => definitions.FindCardFace(id) == null).Select(id => "card face " + id)).ToArray();
            if (missing.Length > 0)
                Framework.Log.Log.WriteWarn("Players own products missing from Configs/Shop; restore them with Listed = false: " +
                    string.Join(", ", missing));
        }
    }
}
