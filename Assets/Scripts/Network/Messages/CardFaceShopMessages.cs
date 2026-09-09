using Game.Network.Accounts;
using Mirror;

namespace Game.Network
{
    public struct CardFaceShopRequest : NetworkMessage
    {
        public ulong RequestId;
        public ShopAction Action;
        public string OperationId;
        public string ProductId;
        public string CatalogRevision;
    }

    public struct CardFaceShopResponse : NetworkMessage
    {
        public ulong RequestId;
        public string AccountId;
        public ShopResult Result;
        public bool HasSnapshot;
        public uint Chips;
        public string[] OwnedIds;
        public string EquippedId;
        public bool CanAct;
        public string CatalogRevision;
        public CardFaceOfferMessage[] Offers;
    }

    public struct CardFaceOfferMessage
    {
        public string Id;
        public uint Price;
        public bool Listed;
    }
}
