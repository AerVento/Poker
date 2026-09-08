using System;
using System.Collections.Generic;
using System.Linq;
using Framework.UI;
using Game.SO;
using Game.Network.Accounts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // Shared by the shop and collection. Emits requests; never deducts chips or consumes inventory locally.
    public sealed class ItemBrowserView : MonoBehaviour
    {
        [SerializeField] private ItemDefinitionSO[] _definitions = Array.Empty<ItemDefinitionSO>();
        [SerializeField] private InventoryItemCard[] _cards;
        [SerializeField] private GameObject _empty;
        [SerializeField] private TextMeshProUGUI _emptyTitle;
        [SerializeField] private TextMeshProUGUI _emptyDescription;
        [SerializeField] private TextMeshProUGUI _detailName;
        [SerializeField] private TextMeshProUGUI _detailKind;
        [SerializeField] private TextMeshProUGUI _detailDescription;
        [SerializeField] private ScrollRect _detailScroll;
        [SerializeField] private TextMeshProUGUI _detailQuantity;
        [SerializeField] private TextMeshProUGUI _actionLabel;
        [SerializeField] private TextMeshProUGUI _pageLabel;
        [SerializeField] private TextMeshProUGUI _countLabel;
        [SerializeField] private TextMeshProUGUI _feedback;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private Button _action;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private Button _goToShop;

        private ItemInventorySnapshot _snapshot = ItemInventorySnapshot.Empty;
        private List<string> _visible = new List<string>();
        private string _selectedId;
        private int _page;
        private int _revision;
        private MsgBox _confirmation;
        private string _pendingOperation;
        private Action<ItemActionRequest> _requestHandler;

        public event Action ShopRequested;
        public event Action<ItemInventorySnapshot> SnapshotChanged;
        public event Action EligibilityRefreshRequested;
        public ShopPageMode Mode { get; private set; }
        public string SelectedId => _selectedId;
        public int CurrentPage => _page;
        public int PageCount => Mathf.Max(1, (_visible.Count + _cards.Length - 1) / _cards.Length);
        public ItemInventorySnapshot Snapshot => _snapshot;

        private void OnEnable()
        {
            _action.onClick.AddListener(ConfirmAction);
            _previous.onClick.AddListener(Previous);
            _next.onClick.AddListener(Next);
            _goToShop.onClick.AddListener(GoToShop);
            Redraw();
        }

        private void OnDisable()
        {
            CancelConfirmation();
            _action.onClick.RemoveListener(ConfirmAction);
            _previous.onClick.RemoveListener(Previous);
            _next.onClick.RemoveListener(Next);
            _goToShop.onClick.RemoveListener(GoToShop);
        }

        public void SetMode(ShopPageMode mode)
        {
            CancelConfirmation();
            Mode = mode;
            _selectedId = null;
            _page = 0;
            _feedback.text = string.Empty;
            Rebuild();
        }

        // Handler must return an authoritative snapshot via CompleteAction, on Unity's main thread.
        public void BindActions(Action<ItemActionRequest> handler)
        {
            CancelConfirmation();
            _requestHandler = handler;
            Redraw();
        }

        public void SetSnapshot(ItemInventorySnapshot snapshot)
        {
            CancelConfirmation();
            _snapshot = snapshot ?? ItemInventorySnapshot.Empty;
            Rebuild();
            SnapshotChanged?.Invoke(_snapshot);
        }

        public void SetFeedback(string message) => _feedback.text = message ?? string.Empty;

        public void SetBalance(uint balance)
        {
            if (_snapshot.Balance == balance) return;
            SetSnapshot(new ItemInventorySnapshot(_snapshot.Items, _snapshot.Offers, balance, _snapshot.CanAct, false));
            EligibilityRefreshRequested?.Invoke();
        }

        public bool CompleteAction(string operationId, ItemInventorySnapshot snapshot, string message)
        {
            if (_pendingOperation == null || operationId != _pendingOperation) return false;
            _pendingOperation = null;
            SetSnapshot(snapshot);
            _feedback.text = message ?? string.Empty;
            return true;
        }

        public void ResetSession()
        {
            _requestHandler = null;
            _pendingOperation = null;
            _selectedId = null;
            _page = 0;
            _feedback.text = string.Empty;
            SetSnapshot(ItemInventorySnapshot.Empty);
        }

        private ItemDefinitionSO Definition(string id) => _definitions.FirstOrDefault(item => item != null && item.Id == id);
        private string NameOf(string id) => Definition(id)?.DisplayName ?? id;
        private string KindOf(string id) => Definition(id) == null ? "物品信息待更新" :
            Definition(id).Kind == ItemKind.Consumable ? "消耗品" : "不可消耗品";

        private void Rebuild()
        {
            _visible = Mode == ShopPageMode.Shop
                ? _snapshot.Offers.Where(offer => offer.IsListed && Definition(offer.ItemId) != null).Select(offer => offer.ItemId).ToList()
                : _snapshot.Items.Where(item => item.Quantity > 0).Select(item => item.ItemId).ToList();
            _page = Mathf.Clamp(_page, 0, PageCount - 1);
            if (!_visible.Contains(_selectedId)) _selectedId = null;
            Redraw();
        }

        public void SelectItem(string id)
        {
            if (!_visible.Contains(id)) return;
            CancelConfirmation();
            _selectedId = id;
            _page = _visible.IndexOf(id) / _cards.Length;
            Redraw();
        }

        public void GoToPage(int page)
        {
            CancelConfirmation();
            _page = Mathf.Clamp(page, 0, PageCount - 1);
            _selectedId = null;
            Redraw();
        }

        private void Previous() => GoToPage(_page - 1);
        private void Next() => GoToPage(_page + 1);
        private void GoToShop() => ShopRequested?.Invoke();

        private string ActionLabel()
        {
            if (_selectedId == null) return "请选择物品";
            if (Mode == ShopPageMode.Collection && Definition(_selectedId)?.Kind == ItemKind.NonConsumable) return "永久收藏";
            if (Mode == ShopPageMode.Shop && Definition(_selectedId)?.Kind == ItemKind.NonConsumable && _snapshot.QuantityOf(_selectedId) > 0) return "已收藏";
            if (_pendingOperation != null) return "正在处理…";
            if (_requestHandler == null || !_snapshot.CanAct || Definition(_selectedId) == null) return "暂不可操作";
            if (Mode == ShopPageMode.Collection) return "使用 1 个";
            var offer = _snapshot.OfferFor(_selectedId);
            if (offer == null) return "暂不可购买";
            var unmet = offer.PurchaseConditions.FirstOrDefault(c => !c.Matches(_snapshot.Balance));
            if (unmet != null) return unmet.RequirementText();
            if (!_snapshot.EligibilityCurrent) return "正在核对购买条件…";
            if (_snapshot.Balance < offer.Price) return "余额不足";
            if (!offer.CanPurchase) return offer.BlockReason switch
            {
                ItemShopResult.QuantityLimit => "持有数量已达上限",
                ItemShopResult.BalanceLimit => "余额将超过上限",
                ItemShopResult.AlreadyOwned => "已收藏",
                _ => "暂不可购买"
            };
            return offer.Price == 0 ? "免费购买" : $"购买 · ${offer.Price:N0}";
        }

        private bool CanAct()
        {
            if (_selectedId == null || _pendingOperation != null || !_snapshot.CanAct || _requestHandler == null) return false;
            var item = Definition(_selectedId);
            if (item == null) return false;
            if (Mode == ShopPageMode.Shop && item.Kind == ItemKind.NonConsumable && _snapshot.QuantityOf(item.Id) > 0) return false;
            if (Mode == ShopPageMode.Collection) return item.Kind == ItemKind.Consumable && _snapshot.QuantityOf(item.Id) > 0;
            var offer = _snapshot.OfferFor(item.Id);
            return offer != null && _snapshot.EligibilityCurrent && offer.CanPurchase &&
                _snapshot.Balance >= offer.Price && _snapshot.QuantityOf(item.Id) < offer.MaxQuantity &&
                offer.PurchaseConditions.All(c => c.Matches(_snapshot.Balance));
        }

        private string OfferDescription(string id)
        {
            var offer = _snapshot.OfferFor(id);
            if (offer == null) return string.Empty;
            var lines = new List<string> { offer.Price == 0 ? "价格：免费（$0）" : $"价格：${offer.Price:N0}" };
            if (offer.RewardChips > 0) lines.Add($"购买后获得 ${offer.RewardChips:N0}");
            lines.AddRange(offer.PurchaseConditions.Select(c => c.RequirementText()));
            if (offer.MaxQuantity == 1 && Definition(id)?.Kind == ItemKind.NonConsumable) lines.Add("每账户限购一次 · 永久收藏");
            return string.Join("\n", lines);
        }

        private void Redraw()
        {
            if (_cards == null || _cards.Length == 0) return;
            bool shop = Mode == ShopPageMode.Shop;
            _empty.SetActive(_visible.Count == 0);
            _emptyTitle.text = shop ? "物品商店即将上新" : "还没有收藏物品";
            _emptyDescription.text = shop ? "新的游戏物品将在这里上架。\n先去名片商店，挑选你的入座风格。" : "购买的物品会收纳在这里。\n消耗品可在详情中使用，其他物品会一直保留。";
            _goToShop.gameObject.SetActive(!shop);
            _pageLabel.text = $"{_page + 1:00}  /  {PageCount:00}";
            _countLabel.text = shop ? $"共 {_visible.Count} 件商品" : $"已收藏 {_visible.Count} 种物品";
            _previous.interactable = _page > 0;
            _next.interactable = _page + 1 < PageCount;
            for (int i = 0; i < _cards.Length; i++)
            {
                int index = _page * _cards.Length + i;
                _cards[i].gameObject.SetActive(index < _visible.Count);
                if (index >= _visible.Count) continue;
                var id = _visible[index];
                _cards[i].Present(NameOf(id), KindOf(id), Definition(id)?.Icon,
                    shop ? _snapshot.OfferFor(id).Price == 0 ? "免费" : $"${_snapshot.OfferFor(id).Price:N0}" : $"× {_snapshot.QuantityOf(id):N0}",
                    id == _selectedId, () => SelectItem(id));
            }
            var selected = Definition(_selectedId);
            _detailName.text = _selectedId == null ? "物品详情" : NameOf(_selectedId);
            _detailKind.text = _selectedId == null ? "选择物品，查看详情" : KindOf(_selectedId);
            _detailDescription.text = _selectedId == null ? "物品介绍、持有数量和可用操作\n会显示在这里。" :
                selected == null ? "当前版本暂时无法显示此物品的详细信息。" : selected.Description;
            if (shop && selected != null) _detailDescription.text = OfferDescription(_selectedId) + "\n\n" + selected.Description;
            if (_detailScroll != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_detailDescription.rectTransform);
                _detailScroll.verticalNormalizedPosition = 1;
            }
            _detailQuantity.text = _selectedId == null ? string.Empty : $"当前拥有  × {_snapshot.QuantityOf(_selectedId):N0}";
            _detailIcon.sprite = selected?.Icon;
            _detailIcon.enabled = selected != null && selected.Icon != null;
            _actionLabel.text = ActionLabel();
            _action.interactable = CanAct();
        }

        private void ConfirmAction()
        {
            if (!CanAct() || !UIManager.Available) return;
            CancelConfirmation();
            var revision = _revision;
            var id = _selectedId;
            bool shop = Mode == ShopPageMode.Shop;
            uint price = shop ? _snapshot.OfferFor(id).Price : 0;
            _confirmation = MsgBox.Create("ItemAction");
            _confirmation.Type = MsgboxType.ConfirmAndCancel;
            _confirmation.Title = shop ? "购买物品" : "使用物品";
            _confirmation.Message = shop ? $"「{NameOf(id)}」× 1\n{OfferDescription(id)}\n购买成功后放入收藏。" : $"使用「{NameOf(id)}」× 1？\n成功后将消耗 1 个。";
            _confirmation.ConfirmButtonText = shop ? "确认购买" : "确认使用";
            _confirmation.CancelButtonText = "取消";
            _confirmation.PreferCancel = true;
            _confirmation.OnCancel += () => _confirmation = null;
            _confirmation.OnConfirm += () =>
            {
                _confirmation = null;
                if (!isActiveAndEnabled || revision != _revision || id != _selectedId || !CanAct()) return;
                var request = new ItemActionRequest(shop ? ItemAction.Purchase : ItemAction.Use, id, price);
                _pendingOperation = request.OperationId;
                _feedback.text = "正在处理，请稍候…";
                Redraw();
                _requestHandler(request);
            };
        }

        private void CancelConfirmation()
        {
            _revision++;
            var confirmation = _confirmation;
            _confirmation = null;
            if (confirmation != null) confirmation.Hide();
        }
    }
}
