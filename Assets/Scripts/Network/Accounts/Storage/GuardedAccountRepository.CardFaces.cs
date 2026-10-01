namespace Game.Network.Accounts
{
    public sealed partial class GuardedAccountRepository : ICardFaceRepository
    {
        public CardFaceInventory ReadCardFaces(string accountId)
            => Execute(() => ((ICardFaceRepository)_inner).ReadCardFaces(accountId));

        public ShopResult ChangeCardFace(string accountId, string operationId, ShopAction action,
            string productId, string catalogRevision, CardFaceServerCatalog catalog)
            => Execute(() => ((ICardFaceRepository)_inner).ChangeCardFace(
                accountId, operationId, action, productId, catalogRevision, catalog));
    }
}
