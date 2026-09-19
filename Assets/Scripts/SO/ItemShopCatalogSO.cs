using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using UnityEngine;

namespace Game.SO
{
    [CreateAssetMenu(fileName = "ItemShopCatalog", menuName = "Poker/Items/Shop Catalog")]
    public sealed class ItemShopCatalogSO : ScriptableObject
    {
        public const string ResourcePath = "SO/Shop/Items/ItemShopCatalog";

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private ItemDefinitionSO _item;
            [SerializeField] private uint _price;
            [SerializeField] private bool _listed = true;
            [SerializeField] private uint _maxQuantity = uint.MaxValue;

            public ItemDefinitionSO Item => _item;
            public uint Price => _price;
            public bool Listed => _listed;
            public uint MaxQuantity => _maxQuantity;
        }

        [SerializeField] private List<Entry> _products = new List<Entry>();
        public IReadOnlyList<Entry> Products => _products;
        public ItemDefinitionSO Find(string id) => _products.FirstOrDefault(p => p?.Item != null && p.Item.Id == id)?.Item;

        public ItemServerCatalog CreateServerCatalog()
        {
            if (_products == null || _products.Any(p => p?.Item == null))
                throw new InvalidOperationException("Item shop catalog contains an empty item reference.");
            var catalog = new ItemServerCatalog
            {
                Products = _products.Select(p => new ServerItemDefinition
                {
                    Id = p.Item.Id,
                    Kind = p.Item.Kind,
                    Price = p.Price,
                    Listed = p.Listed,
                    MaxQuantity = p.MaxQuantity
                }).ToArray()
            };
            catalog.Validate();
            return catalog;
        }

        public static ItemShopCatalogSO LoadDefault()
        {
            var catalog = Resources.Load<ItemShopCatalogSO>(ResourcePath);
            if (catalog == null) throw new InvalidOperationException("Missing item shop catalog SO.");
            return catalog;
        }
    }
}
