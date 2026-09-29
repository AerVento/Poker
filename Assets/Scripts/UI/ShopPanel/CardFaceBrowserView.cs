using System;
using System.Collections.Generic;
using System.Linq;
using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Game.SO;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class CardFaceBrowserView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _labelTemplate;
        [SerializeField] private CardFaceShopCard _cardPrefab;
        [SerializeField] private Button _navigationTemplate;
        private readonly List<GameObject> _drawn = new List<GameObject>();
        private CardFaceShopResponse _snapshot;
        private CardFaceShopRequest _pending;
        private string _accountId;
        private bool _online;
        private int _page;
        private int _generation;
        private int _attempts;
        private double _deadline;
        private MsgBox _confirmation;
        private TextMeshProUGUI _feedback;
        private string _message = "正在读取牌面…";
        private const int PageSize = 4;
        private uint _dollars;
        private uint _availableChips;
        private bool _canExchange;
        public event Action<uint, uint, bool> WalletChanged;

        public void BeginOnline()
        {
            if (_online) return;
            _online = true;
            _accountId = GameController.Instance.State.AccountId;
            var room = NetworkManager.singleton as NetworkRoom;
            if (room != null && room.TryGetCollection(out var collection)) ApplyCollection(collection.CardFaces);
            else room?.LoadCollection();
        }

        private void OnEnable() => Draw();
        private void OnDisable()
        {
            CloseConfirmation();
            _online = false;
            _accountId = null;
            _pending = default;
            _snapshot = default;
            _page = 0;
            _message = "正在读取牌面…";
        }

        public void ApplyCollection(CardFaceShopResponse response)
        {
            if (!_online || _pending.RequestId != 0) return;
            _snapshot = response;
            _dollars = response.Dollars;
            _availableChips = response.AvailableChips;
            _canExchange = response.CanExchange;
            WalletChanged?.Invoke(response.Dollars, response.AvailableChips, response.CanExchange);
            _message = "牌面仅改变你自己看到的效果。";
            Draw();
        }

        public void CancelConfirmation() => CloseConfirmation();
        private void CloseConfirmation()
        {
            _generation++;
            if (_confirmation != null) _confirmation.Hide();
            _confirmation = null;
        }

        private void StartRequest(ShopAction action, string id, bool authorizeChipConversion = false,
            uint authorizedShortfall = 0)
        {
            CloseConfirmation();
            _pending = new CardFaceShopRequest { Action = action, ProductId = id,
                CatalogRevision = _snapshot.CatalogRevision, OperationId = action == ShopAction.Read ? null : Guid.NewGuid().ToString("N"),
                AuthorizeChipConversion = authorizeChipConversion, AuthorizedShortfall = authorizedShortfall };
            _attempts = 0;
            Send();
            Draw();
        }

        private void Send()
        {
            _pending.RequestId = (NetworkManager.singleton as NetworkRoom)?.RequestCardFaceShop(
                _pending.Action, _pending.ProductId, _pending.CatalogRevision, _pending.OperationId,
                _pending.AuthorizeChipConversion, _pending.AuthorizedShortfall) ?? 0;
            _attempts++;
            _deadline = Time.realtimeSinceStartupAsDouble + 8;
            _message = _pending.RequestId == 0 ? "连接不可用，请重新打开牌面商店。" : "正在核对服务器…";
        }

        private void Update()
        {
            if (!_online || _pending.RequestId == 0 || Time.realtimeSinceStartupAsDouble < _deadline) return;
            if (_attempts < 3) Send();
            else { _pending = default; _snapshot.CanAct = false; _message = "响应超时，请重新打开牌面商店核对结果。"; }
            Draw();
        }

        public void Receive(CardFaceShopResponse response)
        {
            if (!_online || !isActiveAndEnabled || response.AccountId != _accountId ||
                response.RequestId == 0 || response.RequestId != _pending.RequestId) return;
            if (response.Result == ShopResult.RateLimited && _attempts < 3)
            {
                _deadline = Time.realtimeSinceStartupAsDouble + 0.3;
                return;
            }
            var action = _pending.Action;
            var productId = _pending.ProductId;
            _pending = default;
            if (response.HasSnapshot)
            {
                _snapshot = response;
                _dollars = response.Dollars;
                _availableChips = response.AvailableChips;
                _canExchange = response.CanExchange;
                WalletChanged?.Invoke(response.Dollars, response.AvailableChips, response.CanExchange);
            }
            else _snapshot.CanAct = false;
            if (response.Result == ShopResult.InsufficientChips) CurrencyFeedback.ShowInsufficientFunds();
            _message = response.Result switch
            {
                ShopResult.Success => action == ShopAction.Purchase ? "已收藏，可选择装备。" :
                    action == ShopAction.Equip ? "已装备，所有扑克牌展示已更新。" : "牌面仅改变你自己看到的效果。",
                ShopResult.AlreadyOwned => "你已拥有这套牌面。",
                ShopResult.InsufficientChips => "余额不足。",
                ShopResult.NotOwned => "请先购买这套牌面。",
                ShopResult.CatalogChanged => "商品信息已更新，请重新确认。",
                ShopResult.QuoteChanged => "余额或价格已变化，请重新确认购买。",
                ShopResult.UnknownProduct => "这套牌面暂不可购买。",
                ShopResult.OutsideRoomRequired => "牌局中无法更换牌面。",
                _ => "操作未完成，请重新打开牌面商店核对。"
            };
            Draw();
        }

        // Presentation-only snapshot for editor previews and UI verification.
        public void Preview(CardFaceShopResponse snapshot)
        {
            _snapshot = snapshot;
            _message = "牌面预览 · 示例库存";
            Draw();
        }

        private void Select(CardFaceProductSO product, CardFaceOfferMessage offer, bool owned)
        {
            if (!_online || !_snapshot.CanAct || _pending.RequestId != 0) return;
            if (owned) { StartRequest(ShopAction.Equip, product.Id); return; }
            CloseConfirmation();
            var generation = _generation;
            var revision = _snapshot.CatalogRevision;
            _confirmation = MsgBox.Create("BuyCardFace");
            _confirmation.Type = MsgboxType.ConfirmAndCancel;
            _confirmation.Title = "收藏牌面";
            _confirmation.Message = $"花费 ${offer.Price:N0} 购买「{product.DisplayName}」？";
            _confirmation.ConfirmButtonText = "确认购买";
            _confirmation.CancelButtonText = "取消";
            _confirmation.PreferCancel = true;
            _confirmation.OnConfirm += () =>
            {
                _confirmation = null;
                if (isActiveAndEnabled && _online && generation == _generation &&
                    revision == _snapshot.CatalogRevision && _pending.RequestId == 0)
                    StartRequest(ShopAction.Purchase, product.Id);
            };
            _confirmation.OnCancel += () => _confirmation = null;
        }



        private void Draw()
        {
            if (_labelTemplate == null || _cardPrefab == null || !isActiveAndEnabled) return;
            foreach (var item in _drawn) if (item != null) { item.SetActive(false); Destroy(item); }
            _drawn.Clear();
            var offers = (_snapshot.Offers ?? Array.Empty<CardFaceOfferMessage>()).ToDictionary(o => o.Id);
            var ownedIds = new HashSet<string>(_snapshot.OwnedIds ?? Array.Empty<string>());
            var products = CardFaceAppearance.Catalog?.Products.Where(p => p != null && p.Cards != null &&
                (CardFaceItemId.IsDefaultOwned(p.Id) || ownedIds.Contains(p.Id) || offers.TryGetValue(p.Id, out var offer) && offer.Listed)).ToArray()
                ?? Array.Empty<CardFaceProductSO>();
            var pages = Mathf.Max(1, (products.Length + PageSize - 1) / PageSize);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            var first = _page * PageSize;
            for (var i = first; i < Mathf.Min(first + PageSize, products.Length); i++)
            {
                var product = products[i];
                var card = Instantiate(_cardPrefab, transform);
                card.name = "CardFace-" + product.Id;
                _drawn.Add(card.gameObject);
                var rect = (RectTransform)card.transform;
                rect.anchoredPosition = new Vector2((i - first) % 2 == 0 ? -374 : 374, (i - first) / 2 == 0 ? 143 : -227);
                var owned = CardFaceItemId.IsDefaultOwned(product.Id) || ownedIds.Contains(product.Id);
                var equipped = product.Id == (string.IsNullOrEmpty(_snapshot.EquippedId) ? "default" : _snapshot.EquippedId);
                var hasOffer = offers.TryGetValue(product.Id, out var offer);
                var canAct = _online && _snapshot.HasSnapshot && (owned ? _snapshot.CanEquip : _snapshot.CanAct) && _pending.RequestId == 0 &&
                    (owned || hasOffer && offer.Listed);
                var canAfford = CurrencySystem.CanAfford(offer.Price, _dollars, _availableChips, _canExchange);
                card.Present(product, offer.Price, canAfford, owned, equipped, canAct,
                    () => Select(product, offer, owned));
            }
            _feedback = Label(_message, transform, new Vector2(0, -515), new Vector2(1160, 40), 21);
            _drawn.Add(_feedback.gameObject);
            var pageLabel = Label($"{_page + 1:00}  /  {pages:00}",
                transform, new Vector2(0, -465), new Vector2(280, 30), 19);
            pageLabel.color = new Color(0.83f, 0.72f, 0.49f);
            var ownedLabel = Label($"已收藏 {products.Count(p => CardFaceItemId.IsDefaultOwned(p.Id) || ownedIds.Contains(p.Id))} / {products.Length}",
                transform, new Vector2(-650, -465), new Vector2(180, 30), 19);
            ownedLabel.color = new Color(0.55f, 0.68f, 0.62f);
            var countLabel = Label(products.Length == 0 ? "暂无上架牌面" :
                $"第 {first + 1}—{Mathf.Min(first + PageSize, products.Length)} 款 · 共 {products.Length} 款",
                transform, new Vector2(650, -465), new Vector2(300, 30), 19);
            countLabel.color = ownedLabel.color;
            _drawn.Add(countLabel.gameObject);
            _drawn.Add(ownedLabel.gameObject);
            _drawn.Add(pageLabel.gameObject);
            var previous = Button("上一页", transform, new Vector2(-200, -465), () => { CloseConfirmation(); _page--; Draw(); });
            previous.interactable = _page > 0;
            var next = Button("下一页", transform, new Vector2(200, -465), () => { CloseConfirmation(); _page++; Draw(); });
            next.interactable = _page + 1 < pages;
            _drawn.Add(previous.gameObject);
            _drawn.Add(next.gameObject);
        }

        private TextMeshProUGUI Label(string text, Transform parent, Vector2 position, Vector2 size, float fontSize)
        {
            var label = Instantiate(_labelTemplate, parent);
            label.name = "Label";
            label.gameObject.SetActive(true);
            label.text = text;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize - 3;
            label.fontSizeMax = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.92f, 0.90f, 0.80f);
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            return label;
        }

        private Button Button(string text, Transform parent, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var button = Instantiate(_navigationTemplate, parent);
            button.gameObject.SetActive(true);
            button.name = text;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            button.GetComponentInChildren<TextMeshProUGUI>().text = text;
            return button;
        }

    }
}
