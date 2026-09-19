using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Gifts
{
    public sealed class GiftMenuView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _menuBlocker;

        [SerializeField]
        private RectTransform _effectRoot;

        [SerializeField]
        private RectTransform _menu;

        [SerializeField]
        private TextMeshProUGUI _menuTitle;

        [SerializeField]
        private TextMeshProUGUI _quantity;

        [SerializeField]
        private TextMeshProUGUI _feedback;

        [SerializeField]
        private Button _giftButton;

        [SerializeField]
        private Button _menuBlockerButton;

        [SerializeField] private GameObject _restrictedOverlay;
        public GameObject RestrictedOverlay => _restrictedOverlay;

        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private Image _giftIcon;
        [SerializeField] private TextMeshProUGUI _page;
        [SerializeField] private TextMeshProUGUI _useCost;
        public Button Previous => _previous;
        public Button Next => _next;
        public Image GiftIcon => _giftIcon;
        public TextMeshProUGUI Page => _page;
        public TextMeshProUGUI UseCost => _useCost;

        public RectTransform MenuBlocker => _menuBlocker;
        public RectTransform EffectRoot => _effectRoot;
        public RectTransform Menu => _menu;
        public TextMeshProUGUI MenuTitle => _menuTitle;
        public TextMeshProUGUI Quantity => _quantity;
        public TextMeshProUGUI Feedback => _feedback;
        public Button GiftButton => _giftButton;
        public Button MenuBlockerButton => _menuBlockerButton;
    }
}
