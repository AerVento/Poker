using System;
using System.Collections.Generic;
using System.Linq;
using Framework.Singleton;
using Game.UI.Gifts;
using UnityEngine;

namespace Game.SO
{
    [CreateAssetMenu(fileName = "GiftCatalogSO", menuName = "Poker/Gifts/Catalog")]
    public sealed class GiftCatalogSO : SingletonSO
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private ItemDefinitionSO _item;
            [SerializeField] private GiftEffect _effectPrefab;
            public ItemDefinitionSO Item => _item;
            public string Id => _item != null ? _item.Id : string.Empty;
            public GiftEffect EffectPrefab => _effectPrefab;
        }

        [SerializeField] private List<Entry> _gifts = new List<Entry>();
        private static GiftCatalogSO _instance;
        public static GiftCatalogSO Instance => _instance != null ? _instance :
            _instance = Resources.Load<GiftCatalogSO>("SO/GiftCatalogSO");
        public IReadOnlyList<Entry> Gifts => _gifts;
        public Entry Find(string id) => _gifts.FirstOrDefault(g => g != null && g.Item != null && g.Id == id);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => _instance = null;
    }
}
