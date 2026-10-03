using System;
using System.Collections.Generic;
using System.Linq;
using Framework.UI;
using Game.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed partial class ShopPanel : SingletonPanel<ShopPanel>
    {
        public const int PageSize = 4;

        [SerializeField] private RectTransform _content;
        [SerializeField] private ShopItemCard[] _cards;
        [SerializeField] private Button _back;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private TextMeshProUGUI _ownedLabel;
        [SerializeField] private BalanceView _balance;
        [SerializeField] private TextMeshProUGUI _pageLabel;
        [SerializeField] private TextMeshProUGUI _countLabel;
        [SerializeField] private TextMeshProUGUI _feedback;

        // Namecard items shown on the namecard tab: owned ones and listed shop offers, in Namecards.json order.
        private List<string> _namecards = new List<string>();
        private InventorySnapshot _inventory = InventorySnapshot.Empty;
        private string _playerName;
        private bool _hasAccount;
        private int _page;
        private int _revision;
        private MsgBox _confirmation;

        public event Action<string> PurchaseRequested;
        public event Action<string> EquipRequested;

        public int CurrentPage => _page;
        public int PageCount => Mathf.Max(1, (_namecards.Count + PageSize - 1) / PageSize);
        public InventorySnapshot Inventory => _inventory;

        private void OnEnable()
        {
            _back.onClick.AddListener(Back);
            _previous.onClick.AddListener(PreviousPage);
            _next.onClick.AddListener(NextPage);
            _page = 0;
            ClientCatalog.Changed += RefreshCatalog;
            RefreshCatalog();
            FitContent();
            EnableTabs();
        }

        private void OnDisable()
        {
            CloseConfirmation();
            ClientCatalog.Changed -= RefreshCatalog;
            DisableTabs();
            _back.onClick.RemoveListener(Back);
            _previous.onClick.RemoveListener(PreviousPage);
            _next.onClick.RemoveListener(NextPage);
            ResetOnline();
            _inventory = InventorySnapshot.Empty;
            _hasAccount = false;
            _playerName = null;
            PurchaseRequested = null;
            EquipRequested = null;
        }

        private void OnRectTransformDimensionsChange() => FitContent();

        private void FitContent()
        {
            if (_content == null)
                return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x > 0 && size.y > 0)
                _content.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
        }

        public void RefreshCatalog()
        {
            CloseConfirmation();
            var catalog = ClientCatalog.Current;
            _namecards = (catalog?.Namecards ?? Array.Empty<NamecardDefinition>())
                .Where(card => catalog.FindItem(card.ItemId) != null && IsVisible(card.ItemId))
                .Select(card => card.ItemId).ToList();
            _page = Mathf.Clamp(_page, 0, PageCount - 1);
            Redraw();
        }

        // A presentation snapshot only. This panel never mutates account balances or ownership.
        public void SetInventory(string playerName, InventorySnapshot inventory)
        {
            CloseConfirmation();
            _playerName = playerName;
            _inventory = inventory ?? InventorySnapshot.Empty;
            _hasAccount = true;
            _itemsView.SetSnapshot(_inventory);
            if (_cardFacesView != null) _cardFacesView.SetSnapshot(_inventory);
            RefreshCatalog();
        }

        public void SetFeedback(string message) => _feedback.text = message ?? string.Empty;

        public void PreviousPage() => GoToPage(_page - 1);
        public void NextPage() => GoToPage(_page + 1);

        public void GoToPage(int page)
        {
            CloseConfirmation();
            _page = Mathf.Clamp(page, 0, PageCount - 1);
            Redraw();
        }

        private bool IsVisible(string itemId) =>
            _inventory.Owns(itemId) || ClientCatalog.FindOffer(itemId)?.Listed == true;

        private bool CanCover(uint amount) =>
            CurrencySystem.CanAfford(amount, _inventory.Dollars, _inventory.AvailableChips, _inventory.CanExchange);

        private void Redraw()
        {
            if (_cards == null || _balance == null)
                return;
            _balance.Present(_hasAccount ? _inventory.Dollars : (uint?)null);
            _pageLabel.text = $"{_page + 1:00}  /  {PageCount:00}";
            int first = _page * PageSize;
            _countLabel.text = _namecards.Count == 0 ? "暂无上架名片" :
                $"第 {first + 1}—{Math.Min(first + PageSize, _namecards.Count)} 款 · 共 {_namecards.Count} 款";
            _previous.interactable = _page > 0;
            _next.interactable = _page + 1 < PageCount;
            _ownedLabel.text = $"已收藏 {_namecards.Count(_inventory.Owns)} / {_namecards.Count}";
            var equippedId = _inventory.Equipped(EquipSlot.Namecard);
            for (int i = 0; i < _cards.Length; i++)
            {
                bool visible = i < PageSize && first + i < _namecards.Count;
                _cards[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var id = _namecards[first + i];
                var offer = ClientCatalog.FindOffer(id);
                bool owned = _inventory.Owns(id);
                bool hasHandler = owned ? EquipRequested != null : PurchaseRequested != null;
                var actionAllowed = owned ? _inventory.CanEquip : _inventory.CanAct;
                _cards[i].Present(ClientCatalog.FindItem(id), ClientCatalog.FindNamecard(id), offer, _playerName,
                    _inventory.Dollars, CanCover(offer?.Price ?? 0), _hasAccount, owned, id == equippedId,
                    actionAllowed && hasHandler && !IsBusy, SelectNamecard);
            }
        }

        private void SelectNamecard(string id)
        {
            if (id == null || ActiveTab != ShopTab.Namecards || !_hasAccount || !_namecards.Contains(id) || IsBusy)
                return;
            if (_inventory.Owns(id))
            {
                if (_inventory.CanEquip && id != _inventory.Equipped(EquipSlot.Namecard))
                    EquipRequested?.Invoke(id);
                return;
            }
            var offer = ClientCatalog.FindOffer(id);
            var item = ClientCatalog.FindItem(id);
            if (!_inventory.CanAct || offer == null || item == null || !offer.CanPurchase || !CanCover(offer.Price) ||
                PurchaseRequested == null || !UIManager.Available)
                return;
            CloseConfirmation();
            var price = offer.Price;
            var revision = _revision;
            _confirmation = MsgBox.Create("NamecardPurchase");
            _confirmation.Type = MsgboxType.ConfirmAndCancel;
            _confirmation.Title = "收藏这张名片";
            _confirmation.Message = $"「{item.Name}」\n价格：${price:N0}\n当前美金：${_inventory.Dollars:N0}";
            if (!Game.Content.RemoteAssets.IsReady(ClientCatalog.FindNamecard(id)?.Artwork))
                _confirmation.Message += "\n外观尚未加载完成，购买后可能暂时无法显示。";
            _confirmation.ConfirmButtonText = "确认购买";
            _confirmation.CancelButtonText = "再看看";
            _confirmation.PreferCancel = true;
            _confirmation.OnCancel += () => _confirmation = null;
            _confirmation.OnConfirm += () =>
            {
                _confirmation = null;
                if (isActiveAndEnabled && _revision == revision && _inventory.CanAct && !IsBusy &&
                    ClientCatalog.FindOffer(id)?.Price == price && CanCover(price) && !_inventory.Owns(id))
                    PurchaseRequested?.Invoke(id);
            };
        }

        private void CloseConfirmation()
        {
            _revision++;
            var confirmation = _confirmation;
            _confirmation = null;
            if (confirmation != null)
                confirmation.Hide();
        }

        private void Back() => UIManager.Instance.PopPanel<ShopPanel>();
    }
}
