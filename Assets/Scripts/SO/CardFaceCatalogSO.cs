using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.SO
{
    [CreateAssetMenu(fileName = "CardFaceCatalog", menuName = "Poker/Card Faces/Catalog")]
    public sealed class CardFaceCatalogSO : ScriptableObject
    {
        [SerializeField] private List<CardFaceProductSO> _products = new List<CardFaceProductSO>();
        public IReadOnlyList<CardFaceProductSO> Products => _products;
        public CardFaceProductSO Find(string id) => _products.FirstOrDefault(p => p != null && p.Id == id);
        public CardFaceProductSO Default => Find("default");
    }
}
