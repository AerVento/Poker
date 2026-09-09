using System;
using Game.SO;
using UnityEngine;

namespace Game.UI
{
    public static class CardFaceAppearance
    {
        private static string _equippedId = "default";
        private static CardFaceCatalogSO _catalog;
        public static event Action Changed;
        public static CardFaceCatalogSO Catalog => _catalog != null ? _catalog :
            _catalog = Resources.Load<CardFaceCatalogSO>("SO/CardFaces/CardFaceCatalog");
        public static CardFaceProductSO Current => Catalog == null ? null : Catalog.Find(_equippedId) ?? Catalog.Default;
        public static string EquippedId => _equippedId;

        // Called only with the local account's private authentication/shop snapshot.
        public static void Equip(string id)
        {
            _equippedId = string.IsNullOrEmpty(id) ? "default" : id;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _equippedId = "default";
            _catalog = null;
            Changed = null;
        }
    }
}
