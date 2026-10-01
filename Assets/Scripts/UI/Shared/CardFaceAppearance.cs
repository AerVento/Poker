using System;
using Game.Network;
using Game.SO;
using UnityEngine;

namespace Game.UI
{
    public static class CardFaceAppearance
    {
        private static string _equippedId = CardFaceDefinition.DefaultId;
        public static event Action Changed;

        /// <summary>The equipped card face from the server catalog; null before the catalog arrives (classic is shown).</summary>
        public static CardFaceDefinition Current =>
            ClientShopCatalog.FindCardFace(_equippedId) ?? ClientShopCatalog.FindCardFace(CardFaceDefinition.DefaultId);
        public static string EquippedId => _equippedId;
        public static CardFaceView ViewPrefab => ShopLocalArtSO.Instance != null ? ShopLocalArtSO.Instance.CardFaceView : null;
        /// <summary>The bundled classic deck, shown for the default face and until a remote deck loads.</summary>
        public static PokerCardsSO ClassicCards => ShopLocalArtSO.Instance != null ? ShopLocalArtSO.Instance.ClassicCards : null;

        // Called only with the local account's private authentication/shop snapshot.
        public static void Equip(string id)
        {
            _equippedId = string.IsNullOrEmpty(id) ? CardFaceDefinition.DefaultId : id;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _equippedId = CardFaceDefinition.DefaultId;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe() => ClientShopCatalog.Changed += () => Changed?.Invoke();
    }
}
