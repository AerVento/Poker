using Game.UI;
using UnityEngine;

namespace Game.SO
{
    /// <summary>
    /// Art that stays in the page: the default namecard, the classic deck and its preview, the shared card face view,
    /// and the gift menu icon shown until the remote item icons arrive. Every other item's art is remote content.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopLocalArt", menuName = "Poker/Shop/Local Art")]
    public sealed class ShopLocalArtSO : ScriptableObject
    {
        public const string ResourcePath = "SO/ShopLocalArt";

        [SerializeField] private Sprite _defaultNamecard;
        [SerializeField] private PokerCardsSO _classicCards;
        [SerializeField] private Sprite _classicPreview;
        [SerializeField] private Sprite _defaultItemIcon;
        [SerializeField] private CardFaceView _cardFaceView;

        public Sprite DefaultNamecard => _defaultNamecard;
        public PokerCardsSO ClassicCards => _classicCards;
        public Sprite ClassicPreview => _classicPreview;
        public Sprite DefaultItemIcon => _defaultItemIcon;
        public CardFaceView CardFaceView => _cardFaceView;

        private static ShopLocalArtSO _instance;
        public static ShopLocalArtSO Instance => _instance != null ? _instance : _instance = Resources.Load<ShopLocalArtSO>(ResourcePath);

        /// <summary>The remote item icon, or the local placeholder until it is downloaded.</summary>
        public static Sprite ItemIcon(ItemDefinition item) =>
            (item != null ? Game.Content.ItemArt.ItemIcon(item.Icon) : null) ?? Instance?.DefaultItemIcon;

        /// <summary>The card face preview; the classic one is local, others are remote and may still be missing.</summary>
        public static Sprite CardFacePreview(CardFaceDefinition face) =>
            face == null ? null : face.IsDefault ? Instance?.ClassicPreview : Game.Content.ItemArt.CardFacePreview(face.Preview);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _instance = null;
    }
}
