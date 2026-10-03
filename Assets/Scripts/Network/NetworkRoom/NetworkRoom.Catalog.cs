namespace Game.Network
{
    public partial class NetworkRoom
    {
        private GameCatalogMessage? _catalogMessage;

        // Built once per server process from Configs/Items and Configs/Shop; every login receives the same public definitions.
        internal GameCatalogMessage CatalogMessage => _catalogMessage ??= ClientCatalog.ToMessage(ServerConfiguration.Current.Catalog);
    }
}
