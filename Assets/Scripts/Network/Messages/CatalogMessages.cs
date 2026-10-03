using Mirror;

namespace Game.Network
{
    // Public item and shop definitions from Configs/Items and Configs/Shop. Sent before the login response, so it carries no account data.
    public struct GameCatalogMessage : NetworkMessage
    {
        public string Revision;
        public ItemDefinitionMessage[] Items;
        public NamecardDefinitionMessage[] Namecards;
        public CardFaceDefinitionMessage[] CardFaces;
        public GiftDefinitionMessage[] Gifts;
        public ContractDefinitionMessage[] Contracts;
        public ServiceNamecardMessage[] ServiceNamecards;
        public ShopOfferMessage[] Shop;
    }

    public struct ItemDefinitionMessage
    {
        public string Id;
        public string Name;
        public string Description;
        public string Icon;
        public bool Consumable;
        public uint MaxQuantity;
    }

    public struct NamecardDefinitionMessage
    {
        public string ItemId;
        public string Artwork;
    }

    public struct CardFaceDefinitionMessage
    {
        public string ItemId;
        public string Cards;
        public string Preview;
    }

    public struct GiftDefinitionMessage
    {
        public string ItemId;
        public string Effect;
    }

    public struct ContractDefinitionMessage
    {
        public string ItemId;
        public uint SignBelowAssets;
        public uint Payout;
        public uint Repayment;
        public uint ServiceYears;
    }

    public struct ServiceNamecardMessage
    {
        public ulong MinServiceYears;
        public string NamecardItemId;
    }

    public struct ShopOfferMessage
    {
        public string ItemId;
        public uint Price;
        public bool Listed;
        public bool Purchasable;
    }
}
