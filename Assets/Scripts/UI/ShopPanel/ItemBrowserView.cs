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
    // Shared by the shop and collection for every item that is not a namecard or card face (those have their own tabs).
    // Emits requests; never deducts money or consumes inventory locally.
    public sealed class ItemBrowserView : MonoBehaviour
    {
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
        [Tooltip("Drawn placeholder shown while the selected item's icon is unavailable.")]
        [SerializeField] private GameObject _detailPlaceholder;
        [SerializeField] private Button _action;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private Button _goToShop;
        [SerializeField] private TMP_InputField _quantityInputPrefab;

        private InventorySnapshot _snapshot = InventorySnapshot.Empty;
        private List<string> _visible = new List<string>();
        private string _selectedId;
        private int _page;
        private int _revision;
        private MsgBox _confirmation;
        private string _pendingOperation;
        private Action<ItemActionRequest> _requestHandler;

        public event Action ShopRequested;
        public ShopPageMode Mode { get; private set; }
        public int CurrentPage => _page;
        public int PageCount => Mathf.Max(1, (_visible.Count + _cards.Length - 1) / _cards.Length);
        public InventorySnapshot Snapshot => _snapshot;

        private void OnEnable()
        {
            _action.onClick.AddListener(ConfirmAction);
            _previous.onClick.AddListener(Previous);
            _next.onClick.AddListener(Next);
            _goToShop.onClick.AddListener(GoToShop);
            ClientCatalog.Changed += Rebuild;
            Game.Content.ItemArt.Changed += Redraw;
            Redraw();
        }

        private void OnDisable()
        {
            CancelConfirmation();
            ClientCatalog.Changed -= Rebuild;
            Game.Content.ItemArt.Changed -= Redraw;
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

        public void SetSnapshot(InventorySnapshot snapshot)
        {
            CancelConfirmation();
            _snapshot = snapshot ?? InventorySnapshot.Empty;
            Rebuild();
        }

        public void ClearLoadingFeedback()
        {
            if (_feedback.text == "正在读取账户、库存与商品价格…") _feedback.text = string.Empty;
        }

        public void SetFeedback(string message) => _feedback.text = message ?? string.Empty;

        private bool CanAfford(ulong amount) =>
            CurrencySystem.CanAfford(amount, _snapshot.Dollars, _snapshot.AvailableChips, _snapshot.CanExchange);

        public bool CompleteAction(string operationId, InventorySnapshot snapshot, string message)
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
            SetSnapshot(InventorySnapshot.Empty);
        }

        private static ItemDefinition Definition(string id) => ClientCatalog.FindItem(id);
        private static ShopOffer Offer(string id) => ClientCatalog.FindOffer(id);
        private static bool IsAppearance(string id)
        {
            var catalog = ClientCatalog.Current;
            return catalog != null && (catalog.FindNamecard(id)?.ItemId == id || catalog.FindCardFace(id)?.ItemId == id);
        }
        private static bool IsGift(string id) => ClientCatalog.FindGift(id) != null;
        // Using a contract repays it; every other usable item would add its cost here.
        private static uint UseCost(string id) => ClientCatalog.Current?.FindContract(id)?.Repayment ?? 0;
        private static uint PurchaseReward(string id) => ClientCatalog.Current?.FindContract(id)?.Payout ?? 0;

        private string NameOf(string id) => Definition(id)?.Name ?? id;
        private string KindOf(string id) => Definition(id) == null ? "物品信息待更新" :
            IsGift(id) ? "礼物" : Definition(id).Consumable ? "消耗品" : "非消耗品";

        private void Rebuild()
        {
            var catalog = ClientCatalog.Current;
            _visible = Mode == ShopPageMode.Shop
                ? (catalog?.Shop ?? Array.Empty<ShopOffer>()).Where(offer => offer.Listed && Definition(offer.ItemId) != null &&
                    !IsAppearance(offer.ItemId)).Select(offer => offer.ItemId).ToList()
                : _snapshot.Items.Where(item => !IsAppearance(item.ItemId)).Select(item => item.ItemId).ToList();
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
            var item = Definition(_selectedId);
            if (Mode == ShopPageMode.Collection && item?.Consumable == false) return "永久收藏";
            if (Mode == ShopPageMode.Shop && item?.MaxQuantity == 1 && _snapshot.Owns(_selectedId)) return "已收藏";
            if (_pendingOperation != null) return "正在处理…";
            // Gifts are consumed by giving them at the table, not from the collection.
            if (Mode == ShopPageMode.Collection && IsGift(_selectedId) && !_snapshot.CanUse(_selectedId)) return "牌局中送出";
            if (!CanAct()) return Mode == ShopPageMode.Shop ? "无法购买" : "无法使用";
            if (Mode == ShopPageMode.Collection) return "使用";
            var price = Offer(_selectedId).Price;
            return price == 0 ? "免费购买" : $"购买 · ${price:N0}";
        }

        private bool CanAct()
        {
            if (_selectedId == null || _pendingOperation != null || !_snapshot.CanAct || _requestHandler == null) return false;
            var item = Definition(_selectedId);
            if (item == null) return false;
            if (Mode == ShopPageMode.Collection)
                return item.Consumable && _snapshot.Owns(item.Id) && _snapshot.EligibilityCurrent &&
                    _snapshot.CanUse(item.Id) && CanAfford(UseCost(item.Id));
            var offer = Offer(item.Id);
            return offer != null && offer.Listed && _snapshot.EligibilityCurrent && _snapshot.OfferState(item.Id)?.CanPurchase == true &&
                CanAfford(offer.Price) && _snapshot.QuantityOf(item.Id) < item.MaxQuantity;
        }

        private string OfferDescription(string id)
        {
            var offer = Offer(id);
            if (offer == null) return string.Empty;
            return offer.Price == 0 ? "免费购买" : $"${offer.Price:N0}";
        }

        private void Redraw()
        {
            if (_cards == null || _cards.Length == 0) return;
            bool shop = Mode == ShopPageMode.Shop;
            _empty.SetActive(_visible.Count == 0);
            _emptyTitle.text = shop ? "物品商店即将上新" : "还没有收藏物品";
            _emptyDescription.text = shop ? "新的珍藏，正在路上。" : "静候你的第一份收藏。";
            _goToShop.gameObject.SetActive(false);
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
                var price = Offer(id)?.Price ?? 0;
                _cards[i].Present(NameOf(id), KindOf(id), Game.Content.ItemArt.ItemIcon(Definition(id)?.Icon),
                    shop ? price == 0 ? string.Empty : $"${price:N0}" : $"× {_snapshot.QuantityOf(id):N0}",
                    id == _selectedId, () => SelectItem(id));
            }
            var selected = Definition(_selectedId);
            _detailName.text = _selectedId == null ? "物品详情" : NameOf(_selectedId);
            _detailKind.text = _selectedId == null ? string.Empty : KindOf(_selectedId);
            _detailDescription.text = _selectedId == null ? string.Empty :
                selected == null ? "当前版本暂时无法显示此物品的详细信息。" : selected.Description;
            if (_detailScroll != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_detailDescription.rectTransform);
                _detailScroll.verticalNormalizedPosition = 1;
            }
            _detailQuantity.gameObject.SetActive(Mode == ShopPageMode.Collection);
            _detailQuantity.text = _selectedId == null ? string.Empty : $"当前拥有  × {_snapshot.QuantityOf(_selectedId):N0}";
            _detailIcon.sprite = Game.Content.ItemArt.ItemIcon(selected?.Icon);
            _detailIcon.enabled = _detailIcon.sprite != null;
            if (_detailPlaceholder != null) _detailPlaceholder.SetActive(!_detailIcon.enabled);
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
            uint price = shop ? Offer(id).Price : 0;
            var item = Definition(id);
            // Several at once for plain consumables; an item with a purchase effect (a contract) is bought one at a time.
            bool bulkPurchase = shop && item.Consumable && PurchaseReward(id) == 0;
            uint maximumQuantity = 1;
            if (bulkPurchase)
            {
                var remaining = item.MaxQuantity - _snapshot.QuantityOf(id);
                var purchasingPower = CurrencySystem.PurchasingPower(_snapshot.Dollars, _snapshot.AvailableChips, _snapshot.CanExchange);
                var affordable = price == 0 ? remaining : Math.Min(uint.MaxValue, purchasingPower / price);
                maximumQuantity = Math.Max(1, Math.Min(remaining, (uint)affordable));
            }
            _confirmation = MsgBox.Create("ItemAction");
            _confirmation.Type = MsgboxType.ConfirmAndCancel;
            _confirmation.Title = shop ? "购买物品" : "使用物品";
            var useCost = UseCost(id);
            _confirmation.Message = shop ? bulkPurchase
                    ? BulkPurchaseMessage(id, maximumQuantity, price, 1)
                    : $"{NameOf(id)} × 1\n{OfferDescription(id)}" :
                $"使用 {NameOf(id)} × 1？" + (useCost > 0 ? $"\n支付 ${useCost:N0}" : string.Empty);
            var quantityInput = bulkPurchase ? CreateQuantityInput(_confirmation, maximumQuantity) : null;
            if (quantityInput != null)
            {
                quantityInput.onValueChanged.AddListener(value =>
                {
                    if (_confirmation == null)
                        return;
                    if (!uint.TryParse(value, out var quantity))
                        quantity = 1;
                    quantity = Math.Max(1u, Math.Min(quantity, maximumQuantity));
                    if (!string.IsNullOrEmpty(value))
                        quantityInput.SetTextWithoutNotify(quantity.ToString());
                    _confirmation.Message = BulkPurchaseMessage(id, maximumQuantity, price, quantity);
                });
            }
            _confirmation.ConfirmButtonText = shop ? "购买" : "使用";
            _confirmation.CancelButtonText = "取消";
            _confirmation.PreferCancel = true;
            _confirmation.OnCancel += () => _confirmation = null;
            _confirmation.OnConfirm += () =>
            {
                _confirmation = null;
                if (!isActiveAndEnabled || revision != _revision || id != _selectedId || !CanAct()) return;
                uint quantity = 1;
                if (quantityInput != null)
                {
                    if (!uint.TryParse(quantityInput.text, out quantity))
                        quantity = 1;
                    quantity = Math.Max(1u, Math.Min(quantity, maximumQuantity));
                }
                var request = new ItemActionRequest(shop ? ItemAction.Purchase : ItemAction.Use, id, price, quantity);
                _pendingOperation = request.OperationId;
                _feedback.text = "正在处理，请稍候…";
                Redraw();
                _requestHandler(request);
            };
        }

        private string BulkPurchaseMessage(string id, uint maximumQuantity, uint price, uint quantity)
        {
            return $"{NameOf(id)}\n数量 {quantity:N0} / {maximumQuantity:N0}\n" +
                $"合计 ${(ulong)price * quantity:N0}（单价 ${price:N0}）";
        }

        private TMP_InputField CreateQuantityInput(MsgBox confirmation, uint maximumQuantity)
        {
            var input = Instantiate(_quantityInputPrefab, confirmation.transform, false);
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = maximumQuantity.ToString().Length;
            input.text = "1";
            input.GetComponent<QuantityStepper>()?.Configure(maximumQuantity);
            input.caretColor = Color.white;
            input.selectionColor = new Color(0.3f, 0.65f, 0.45f, 0.75f);
            input.onEndEdit.AddListener(value => NormalizeQuantity(input, value, maximumQuantity));
            input.Select();
            input.ActivateInputField();
            return input;
        }

        private static void NormalizeQuantity(TMP_InputField input, string value, uint maximumQuantity)
        {
            if (!uint.TryParse(value, out var quantity))
                quantity = 1;
            input.SetTextWithoutNotify(Math.Max(1u, Math.Min(quantity, maximumQuantity)).ToString());
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
