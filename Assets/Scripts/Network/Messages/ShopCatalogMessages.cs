using Mirror;

namespace Game.Network
{
    // Public product definitions from Configs/Shop. Sent before the login response, so it carries no account data.
    public struct ShopCatalogMessage : NetworkMessage
    {
        public string Revision;
        public ItemDefinitionMessage[] Items;
        public GiftDefinitionMessage[] Gifts;
        public NamecardDefinitionMessage[] Namecards;
        public CardFaceDefinitionMessage[] CardFaces;
    }

    public struct ItemDefinitionMessage
    {
        public string Id;
        public string Name;
        public string Description;
        public string Icon;
        public ItemKind Kind;
        public uint Price;
        public bool Listed;
        public uint MaxQuantity;
    }

    public struct GiftDefinitionMessage
    {
        public string ItemId;
        public string Effect;
    }

    public struct NamecardDefinitionMessage
    {
        public string Id;
        public string Name;
        public string Description;
        public string Artwork;
        public uint Price;
        public bool Listed;
        public bool Purchasable;
        public bool Reward;
    }

    public struct CardFaceDefinitionMessage
    {
        public string Id;
        public string Name;
        public string Description;
        public string Cards;
        public string Preview;
        public uint Price;
        public bool Listed;
    }
}
