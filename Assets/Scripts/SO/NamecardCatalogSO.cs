using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using UnityEngine;

namespace Game.SO
{
    [CreateAssetMenu(fileName = "NamecardCatalog", menuName = "Poker/Shop/Namecard Catalog")]
    public sealed class NamecardCatalogSO : ScriptableObject
    {
        public const string ResourcePath = "SO/Shop/Namecards/NamecardCatalog";

        [Tooltip("Listed products, in display order. Keep delisted products here so owners can equip them.")]
        [SerializeField] private NamecardProductSO[] _products = Array.Empty<NamecardProductSO>();

        public IReadOnlyList<NamecardProductSO> Products => _products;

        public NamecardProductSO GetProduct(string id)
        {
            if (string.IsNullOrEmpty(id))
                id = "default";
            foreach (var product in _products ?? Array.Empty<NamecardProductSO>())
            {
                if (product != null && string.Equals(product.Id, id, StringComparison.Ordinal))
                    return product;
            }
            return null;
        }

        public Sprite GetArtwork(string id, float targetAspect)
        {
            var product = GetProduct(id);
            return product != null ? product.GetArtwork(targetAspect) : null;
        }

        public List<NamecardProductSO> GetListedProducts()
        {
            var result = new List<NamecardProductSO>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var product in _products ?? Array.Empty<NamecardProductSO>())
            {
                if (product != null && !string.IsNullOrWhiteSpace(product.Id) && ids.Add(product.Id))
                    result.Add(product);
            }
            return result;
        }

        public NamecardServerCatalog CreateServerCatalog()
        {
            if (_products == null || _products.Any(product => product == null))
                throw new InvalidOperationException("Namecard shop catalog contains an empty product reference.");
            var catalog = new NamecardServerCatalog
            {
                Products = _products.Select(product => new NamecardOffer
                {
                    Id = product.Id,
                    Price = product.Price,
                    Listed = product.IsListed && product.CanPurchase
                }).ToArray()
            };
            catalog.Validate();
            return catalog;
        }

        public static NamecardCatalogSO LoadDefault()
        {
            var catalog = Resources.Load<NamecardCatalogSO>(ResourcePath);
            if (catalog == null) throw new InvalidOperationException("Missing namecard shop catalog SO.");
            return catalog;
        }
    }
}
