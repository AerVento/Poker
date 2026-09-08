using System;
using System.Collections.Generic;
using Framework.UI;
using Game.SO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed partial class ShopPanel : SingletonPanel<ShopPanel>
    {
        public const int PageSize = 4;

        [SerializeField] private NamecardCatalogSO _catalog;
        [SerializeField] private RectTransform _content;
        [SerializeField] private ShopItemCard[] _cards;
        [SerializeField] private Button _back;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private TextMeshProUGUI _ownedLabel;
        [SerializeField] private TextMeshProUGUI _balance;
        [SerializeField] private TextMeshProUGUI _pageLabel;
        [SerializeField] private TextMeshProUGUI _countLabel;
        [SerializeField] private TextMeshProUGUI _feedback;

        private List<NamecardProductSO> _products = new List<NamecardProductSO>();
        private readonly HashSet<string> _owned = new HashSet<string>(StringComparer.Ordinal);
        private string _equippedId;
        private string _playerName;
        private uint _chips;
        private bool _hasAccount;
        private bool _canAct;
        private int _page;
        private int _revision;
        private MsgBox _confirmation;

        public event Action<NamecardProductSO> PurchaseRequested;
        public event Action<string> EquipRequested;

        public int CurrentPage => _page;
        public int PageCount => Mathf.Max(1, (_products.Count + PageSize - 1) / PageSize);
        public NamecardCatalogSO Catalog => _catalog;

        private void OnEnable()
        {
            _back.onClick.AddListener(Back);
            _previous.onClick.AddListener(PreviousPage);
            _next.onClick.AddListener(NextPage);
            _page = 0;
            RefreshCatalog();
            FitContent();
            EnableTabs();
        }

        private void OnDisable()
        {
            CloseConfirmation();
            DisableTabs();
            _back.onClick.RemoveListener(Back);
            _previous.onClick.RemoveListener(PreviousPage);
            _next.onClick.RemoveListener(NextPage);
            ResetOnline();
            _owned.Clear();
            _hasAccount = false;
            _canAct = false;
            _equippedId = null;
            _playerName = null;
            _chips = 0;
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
            _products = _catalog == null ? new List<NamecardProductSO>() : _catalog.GetListedProducts();
            _products.RemoveAll(product => !IsVisible(product));
            _page = Mathf.Clamp(_page, 0, PageCount - 1);
            Redraw();
        }

        // A presentation snapshot only. This panel never mutates account chips or ownership.
        public void SetAccountView(string playerName, uint chips, IEnumerable<string> ownedIds,
            string equippedId, bool canAct)
        {
            CloseConfirmation();
            _playerName = playerName;
            _chips = chips;
            _owned.Clear();
            if (ownedIds != null)
                _owned.UnionWith(ownedIds);
            _equippedId = equippedId;
            _hasAccount = true;
            _canAct = canAct;
            _itemsView.SetBalance(chips);
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

        private void Redraw()
        {
            if (_cards == null || _balance == null)
                return;
            _balance.text = _hasAccount ? $"${_chips:N0}" : "—";
            _pageLabel.text = $"{_page + 1:00}  /  {PageCount:00}";
            int first = _page * PageSize;
            _countLabel.text = _products.Count == 0 ? "暂无上架名片" :
                $"第 {first + 1}—{Math.Min(first + PageSize, _products.Count)} 款 · 共 {_products.Count} 款";
            _previous.interactable = _page > 0;
            _next.interactable = _page + 1 < PageCount;
            int ownedCount = _products.FindAll(product => product.IsDefault || _owned.Contains(product.Id)).Count;
            _ownedLabel.text = $"已收藏 {ownedCount} / {_products.Count}";
            for (int i = 0; i < _cards.Length; i++)
            {
                bool visible = i < PageSize && first + i < _products.Count;
                _cards[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var product = _products[first + i];
                bool owned = product.IsDefault || _owned.Contains(product.Id);
                bool equipped = product.IsDefault ? string.IsNullOrEmpty(_equippedId) || product.Id == _equippedId :
                    product.Id == _equippedId;
                bool hasHandler = owned ? EquipRequested != null : PurchaseRequested != null;
                _cards[i].Present(product, _playerName, _chips, _hasAccount, owned,
                    equipped, _canAct && hasHandler && HasOffer(product), SelectProduct, PriceOf(product));
            }
        }

        private void SelectProduct(NamecardProductSO product)
        {
            if (product == null || !product.CanPurchase || ActiveTab != ShopTab.Namecards || !_hasAccount || !_canAct || !_products.Contains(product))
                return;
            if (product.IsDefault)
            {
                if (!string.IsNullOrEmpty(_equippedId) && _equippedId != product.Id)
                    EquipRequested?.Invoke(string.Empty);
                return;
            }
            if (_owned.Contains(product.Id))
            {
                if (product.Id != _equippedId)
                    EquipRequested?.Invoke(product.Id);
                return;
            }
            if (_chips < PriceOf(product) || PurchaseRequested == null || !UIManager.Available)
                return;
            CloseConfirmation();
            var price = PriceOf(product);
            var revision = _revision;
            _confirmation = MsgBox.Create("NamecardPurchase");
            _confirmation.Type = MsgboxType.ConfirmAndCancel;
            _confirmation.Title = "收藏这张名片";
            _confirmation.Message = $"「{product.DisplayName}」\n价格：${price:N0}\n购买后余额：${_chips - price:N0}\n永久拥有，购买后可自行装备。";
            _confirmation.ConfirmButtonText = "确认购买";
            _confirmation.CancelButtonText = "再看看";
            _confirmation.PreferCancel = true;
            _confirmation.OnCancel += () => _confirmation = null;
            _confirmation.OnConfirm += () =>
            {
                _confirmation = null;
                if (isActiveAndEnabled && _revision == revision && _canAct &&
                    product != null && PriceOf(product) == price && _chips >= price && !_owned.Contains(product.Id))
                    PurchaseRequested?.Invoke(product);
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
