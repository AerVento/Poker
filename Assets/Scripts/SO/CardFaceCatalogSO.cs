using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using Game.UI;
using UnityEngine;

namespace Game.SO
{
    [CreateAssetMenu(fileName = "CardFaceCatalog", menuName = "Poker/Card Faces/Catalog")]
    public sealed class CardFaceCatalogSO : ScriptableObject
    {
        public const string ResourcePath = "SO/Shop/CardFaces/CardFaceCatalog";

        [SerializeField] private List<CardFaceProductSO> _products = new List<CardFaceProductSO>();
        [SerializeField] private CardFaceView _viewPrefab;
        public CardFaceView ViewPrefab => _viewPrefab;
        public IReadOnlyList<CardFaceProductSO> Products => _products;
        public CardFaceProductSO Find(string id) => _products.FirstOrDefault(p => p != null && p.Id == id);
        public CardFaceProductSO Default => Find("default");

        public CardFaceServerCatalog CreateServerCatalog()
        {
            if (_products == null || _products.Any(product => product == null))
                throw new InvalidOperationException("Card face shop catalog contains an empty product reference.");
            var catalog = new CardFaceServerCatalog
            {
                Products = _products.Select(product => new CardFaceOffer
                {
                    Id = product.Id,
                    Price = product.Price,
                    Listed = product.IsListed
                }).ToArray()
            };
            catalog.Validate();
            return catalog;
        }

        public static CardFaceCatalogSO LoadDefault()
        {
            var catalog = Resources.Load<CardFaceCatalogSO>(ResourcePath);
            if (catalog == null) throw new InvalidOperationException("Missing card face shop catalog SO.");
            return catalog;
        }
    }
}
