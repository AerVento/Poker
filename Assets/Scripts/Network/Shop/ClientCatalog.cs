using System;
using System.Linq;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// The item and shop catalog this client received from the server. Every name, role, art address and price shown
    /// in the shop, collection, gift menu, namecards, card faces and rewards comes from here.
    /// </summary>
    public static class ClientCatalog
    {
        private static GameCatalog _current;
        private static string _revision;

        public static GameCatalog Current => _current;
        public static bool IsAvailable => _current != null;
        public static string Revision => _revision;
        public static event Action Changed;

        public static ItemDefinition FindItem(string id) => _current?.FindItem(id);
        public static NamecardDefinition FindNamecard(string itemId) => _current?.FindNamecard(itemId);
        public static CardFaceDefinition FindCardFace(string itemId) => _current?.FindCardFace(itemId);
        public static GiftDefinition FindGift(string itemId) => _current?.FindGift(itemId);
        public static ShopOffer FindOffer(string itemId) => _current?.FindOffer(itemId);

        public static GameCatalogMessage ToMessage(GameCatalog catalog) => new GameCatalogMessage
        {
            Revision = catalog.Revision,
            Items = catalog.Items.Select(i => new ItemDefinitionMessage
            {
                Id = i.Id, Name = i.Name, Description = i.Description, Icon = i.Icon, Consumable = i.Consumable, MaxQuantity = i.MaxQuantity
            }).ToArray(),
            Namecards = catalog.Namecards.Select(n => new NamecardDefinitionMessage { ItemId = n.ItemId, Artwork = n.Artwork }).ToArray(),
            CardFaces = catalog.CardFaces.Select(c => new CardFaceDefinitionMessage { ItemId = c.ItemId, Cards = c.Cards, Preview = c.Preview }).ToArray(),
            Gifts = catalog.Gifts.Select(g => new GiftDefinitionMessage { ItemId = g.ItemId, Effect = g.Effect }).ToArray(),
            Contracts = catalog.Contracts.Select(c => new ContractDefinitionMessage
            {
                ItemId = c.ItemId, SignBelowAssets = c.SignBelowAssets, Payout = c.Payout, Repayment = c.Repayment, ServiceYears = c.ServiceYears
            }).ToArray(),
            ServiceNamecards = catalog.ServiceNamecards.Select(s => new ServiceNamecardMessage
            {
                MinServiceYears = s.MinServiceYears, NamecardItemId = s.NamecardItemId
            }).ToArray(),
            Shop = catalog.Shop.Select(o => new ShopOfferMessage
            {
                ItemId = o.ItemId, Price = o.Price, Listed = o.Listed, Purchasable = o.Purchasable
            }).ToArray(),
        };

        public static GameCatalog FromMessage(GameCatalogMessage message) => new GameCatalog
        {
            Items = (message.Items ?? Array.Empty<ItemDefinitionMessage>()).Select(i => new ItemDefinition
            {
                Id = i.Id, Name = i.Name, Description = i.Description ?? string.Empty, Icon = i.Icon, Consumable = i.Consumable, MaxQuantity = i.MaxQuantity
            }).ToArray(),
            Namecards = (message.Namecards ?? Array.Empty<NamecardDefinitionMessage>()).Select(n => new NamecardDefinition
            {
                ItemId = n.ItemId, Artwork = n.Artwork ?? string.Empty
            }).ToArray(),
            CardFaces = (message.CardFaces ?? Array.Empty<CardFaceDefinitionMessage>()).Select(c => new CardFaceDefinition
            {
                ItemId = c.ItemId, Cards = c.Cards ?? string.Empty, Preview = c.Preview
            }).ToArray(),
            Gifts = (message.Gifts ?? Array.Empty<GiftDefinitionMessage>()).Select(g => new GiftDefinition { ItemId = g.ItemId, Effect = g.Effect }).ToArray(),
            Contracts = (message.Contracts ?? Array.Empty<ContractDefinitionMessage>()).Select(c => new ContractDefinition
            {
                ItemId = c.ItemId, SignBelowAssets = c.SignBelowAssets, Payout = c.Payout, Repayment = c.Repayment, ServiceYears = c.ServiceYears
            }).ToArray(),
            ServiceNamecards = (message.ServiceNamecards ?? Array.Empty<ServiceNamecardMessage>()).Select(s => new ServiceNamecardDefinition
            {
                MinServiceYears = s.MinServiceYears, NamecardItemId = s.NamecardItemId
            }).ToArray(),
            Shop = (message.Shop ?? Array.Empty<ShopOfferMessage>()).Select(o => new ShopOffer
            {
                ItemId = o.ItemId, Price = o.Price, Listed = o.Listed, Purchasable = o.Purchasable
            }).ToArray(),
        };

        public static void Apply(GameCatalogMessage message)
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
