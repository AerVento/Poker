using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public enum ShopPageMode { Shop, Collection }
    public enum ShopTab { Items, Namecards }

    public sealed partial class ShopPanel
    {
        [SerializeField] private Button _itemsTab;
        [SerializeField] private Button _namecardsTab;
        [SerializeField] private Button _switchMode;
        [SerializeField] private TextMeshProUGUI _itemsTabLabel;
        [SerializeField] private TextMeshProUGUI _namecardsTabLabel;
        [SerializeField] private TextMeshProUGUI _switchModeLabel;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _subtitle;
        [SerializeField] private GameObject _namecardsRoot;
        [SerializeField] private ItemBrowserView _itemsView;

        public ShopPageMode Mode { get; private set; }
        public ShopTab ActiveTab { get; private set; }
        public ItemBrowserView ItemsView => _itemsView;

        private void EnableTabs()
        {
            _itemsTab.onClick.AddListener(ShowItems);
            _namecardsTab.onClick.AddListener(ShowNamecards);
            _switchMode.onClick.AddListener(SwitchMode);
            _itemsView.ShopRequested += ShowShop;
            _itemsView.SnapshotChanged += OnItemSnapshotChanged;
            Open(ShopPageMode.Shop);
        }

        private void DisableTabs()
        {
            _itemsTab.onClick.RemoveListener(ShowItems);
            _namecardsTab.onClick.RemoveListener(ShowNamecards);
            _switchMode.onClick.RemoveListener(SwitchMode);
            _itemsView.ShopRequested -= ShowShop;
            _itemsView.SnapshotChanged -= OnItemSnapshotChanged;
            _itemsView.ResetSession();
        }

        // Both title entries reuse this registered panel and the same online namecard session.
        public void Open(ShopPageMode mode)
        {
            CloseConfirmation();
            Mode = mode;
            _page = 0;
            _itemsView.SetMode(mode);
            SelectTab(ShopTab.Items);
        }

        public void SelectTab(ShopTab tab)
        {
            CloseConfirmation();
            ActiveTab = tab;
            bool items = tab == ShopTab.Items;
            _itemsView.gameObject.SetActive(items);
            _namecardsRoot.SetActive(!items);
            _itemsTab.interactable = !items;
            _namecardsTab.interactable = items;
            _itemsTabLabel.text = Mode == ShopPageMode.Shop ? "物品商店" : "物品";
            _namecardsTabLabel.text = Mode == ShopPageMode.Shop ? "名片商店" : "名片";
            SetTabAppearance(_itemsTab, _itemsTabLabel, items);
            SetTabAppearance(_namecardsTab, _namecardsTabLabel, !items);
            _switchModeLabel.text = Mode == ShopPageMode.Shop ? "我的收藏" : "前往商城";
            _title.text = items ? Mode == ShopPageMode.Shop ? "物品商店" : "我的收藏" : "名片商店";
            _subtitle.text = items ? Mode == ShopPageMode.Shop ? "挑选游戏物品，丰富你的牌桌时光。" :
                "收纳你的物品，也收藏你的风格。" : "购买新名片，或装备你已拥有的款式。";
            if (!items) RefreshCatalog();
        }

        private static void SetTabAppearance(Button button, TextMeshProUGUI label, bool selected)
        {
            button.image.color = selected ? new Color(0.76f, 0.65f, 0.40f) : new Color(0.12f, 0.23f, 0.18f);
            label.color = selected ? new Color(0.06f, 0.12f, 0.09f) : new Color(0.90f, 0.92f, 0.87f);
        }

        private void ShowItems() => SelectTab(ShopTab.Items);
        private void ShowNamecards() => SelectTab(ShopTab.Namecards);
        private void ShowShop() => Open(ShopPageMode.Shop);
        private void SwitchMode() => Open(Mode == ShopPageMode.Shop ? ShopPageMode.Collection : ShopPageMode.Shop);

        private void OnItemSnapshotChanged(ItemInventorySnapshot snapshot)
        {
            CloseConfirmation();
            _chips = snapshot.Balance;
            Redraw();
        }
    }
}
