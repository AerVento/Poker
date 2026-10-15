using System;
using Game.Network;
using Game.SO;
using UnityEngine;

namespace Game.UI
{
    public static class CardFaceAppearance
    {
        private static string _equippedId = CardFaceDefinition.DefaultItemId;
        public static event Action Changed;

        /// <summary>The equipped card face from the server catalog; null before the catalog arrives (classic is shown).</summary>
        public static CardFaceDefinition Current =>
            ClientCatalog.FindCardFace(_equippedId) ?? ClientCatalog.FindCardFace(CardFaceDefinition.DefaultItemId);
        public static CardFaceView ViewPrefab => ShopLocalArtSO.Instance != null ? ShopLocalArtSO.Instance.CardFaceView : null;
        /// <summary>The bundled classic deck, shown for the default face and until a remote deck loads.</summary>
        public static PokerCardsSO ClassicCards => ShopLocalArtSO.Instance != null ? ShopLocalArtSO.Instance.ClassicCards : null;

        // Called only with the local account's private authentication/shop snapshot.
        public static void Equip(string id)
        {
            _equippedId = string.IsNullOrEmpty(id) ? CardFaceDefinition.DefaultItemId : id;
            EquippedArtwork.PreloadCardFace(_equippedId);
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _equippedId = CardFaceDefinition.DefaultItemId;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe() => ClientCatalog.Changed += () => Changed?.Invoke();
    }
}
