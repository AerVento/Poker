namespace Game.Network.Accounts
{
    // Separate interface retains compatibility with existing non-wallet repository adapters.
    public interface IWalletPaymentRepository
    {
        ShopResult ChangeNamecard(string accountId, string operationId, ShopAction action, string productId,
            string catalogRevision, NamecardServerCatalog catalog, bool authorizeChipConversion, uint authorizedShortfall);
        ShopResult ChangeCardFace(string accountId, string operationId, ShopAction action, string productId,
            string catalogRevision, CardFaceServerCatalog catalog, bool authorizeChipConversion, uint authorizedShortfall);
        ItemChangeExecution ChangeItem(string accountId, string operationId, ItemShopAction action, string itemId,
            uint quantity, uint quotedPrice, string catalogRevision, ItemPurchaseRuntime runtime, bool inRoom,
            bool authorizeChipConversion, uint authorizedShortfall);
    }
}
