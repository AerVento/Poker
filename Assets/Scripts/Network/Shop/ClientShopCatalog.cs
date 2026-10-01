using System;
using System.Linq;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// The product definitions this client received from the server. Shop, collection, gift menu, namecards,
    /// card faces and reward labels read products from here; until it arrives only local defaults are shown.
    /// </summary>
    public static class ClientShopCatalog
    {
        private static ShopDefinitions _current;
        private static string _revision;

        public static ShopDefinitions Current => _current;
        public static bool IsAvailable => _current != null;
        public static string Revision => _revision;
        public static event Action Changed;

        public static ItemDefinition FindItem(string id) => _current?.FindItem(id);
        public static GiftDefinition FindGift(string itemId) => _current?.FindGift(itemId);
        public static NamecardDefinition FindNamecard(string id) => _current?.FindNamecard(id);
        public static CardFaceDefinition FindCardFace(string id) => _current?.FindCardFace(id);

        public static ShopCatalogMessage ToMessage(ShopDefinitions definitions) => new ShopCatalogMessage
        {
            Revision = definitions.Revision,
            Items = definitions.Items.Select(i => new ItemDefinitionMessage
            {
                Id = i.Id, Name = i.Name, Description = i.Description, Icon = i.Icon, Kind = i.Kind,
                Price = i.Price, Listed = i.Listed, MaxQuantity = i.MaxQuantity
            }).ToArray(),
            Gifts = definitions.Gifts.Select(g => new GiftDefinitionMessage { ItemId = g.ItemId, Effect = g.Effect }).ToArray(),
            Namecards = definitions.Namecards.Select(n => new NamecardDefinitionMessage
            {
                Id = n.Id, Name = n.Name, Description = n.Description, Artwork = n.Artwork, Price = n.Price,
                Listed = n.Listed, Purchasable = n.Purchasable, Reward = n.Reward
            }).ToArray(),
            CardFaces = definitions.CardFaces.Select(c => new CardFaceDefinitionMessage
            {
                Id = c.Id, Name = c.Name, Description = c.Description, Cards = c.Cards, Preview = c.Preview,
                Price = c.Price, Listed = c.Listed
            }).ToArray(),
        };

        public static ShopDefinitions FromMessage(ShopCatalogMessage message) => new ShopDefinitions
        {
            Items = (message.Items ?? Array.Empty<ItemDefinitionMessage>()).Select(i => new ItemDefinition
            {
                Id = i.Id, Name = i.Name, Description = i.Description ?? string.Empty, Icon = i.Icon, Kind = i.Kind,
                Price = i.Price, Listed = i.Listed, MaxQuantity = i.MaxQuantity
            }).ToArray(),
            Gifts = (message.Gifts ?? Array.Empty<GiftDefinitionMessage>()).Select(g => new GiftDefinition { ItemId = g.ItemId, Effect = g.Effect }).ToArray(),
            Namecards = (message.Namecards ?? Array.Empty<NamecardDefinitionMessage>()).Select(n => new NamecardDefinition
            {
                Id = n.Id, Name = n.Name, Description = n.Description ?? string.Empty, Artwork = n.Artwork ?? string.Empty,
                Price = n.Price, Listed = n.Listed, Purchasable = n.Purchasable, Reward = n.Reward
            }).ToArray(),
            CardFaces = (message.CardFaces ?? Array.Empty<CardFaceDefinitionMessage>()).Select(c => new CardFaceDefinition
            {
                Id = c.Id, Name = c.Name, Description = c.Description ?? string.Empty, Cards = c.Cards ?? string.Empty,
                Preview = c.Preview, Price = c.Price, Listed = c.Listed
            }).ToArray(),
        };

        public static void Apply(ShopCatalogMessage message)
        {
            if (_current != null && message.Revision == _revision) return;
            _current = FromMessage(message);
            _revision = message.Revision;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _current = null;
            _revision = null;
            Changed = null;
        }
    }
}
