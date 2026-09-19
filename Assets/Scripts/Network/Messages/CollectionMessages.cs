using Mirror;

namespace Game.Network
{
    public struct CollectionRequest : NetworkMessage
    {
        public ulong RequestId;
    }

    public struct CollectionResponse : NetworkMessage
    {
        public ulong RequestId;
        public ulong Revision;
        public string AccountId;
        public bool Success;
        public ItemShopResponse Items;
        public NamecardShopResponse Namecards;
        public CardFaceShopResponse CardFaces;
    }
}
