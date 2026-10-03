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
    // The shop panel owns the request pipeline; this view only shows card faces and asks for purchases and equips.
    public sealed class CardFaceBrowserView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _labelTemplate;
        [SerializeField] private CardFaceShopCard _cardPrefab;
        [SerializeField] private Button _navigationTemplate;
        private readonly List<GameObject> _drawn = new List<GameObject>();
        private InventorySnapshot _snapshot = InventorySnapshot.Empty;
        private bool _hasSnapshot;
        private int _page;
        private int _generation;
        private MsgBox _confirmation;
        private TextMeshProUGUI _feedback;
        private string _message = "正在读取牌面…";
        private const int PageSize = 4;

        public event Action<string> PurchaseRequested;
        public event Action<string> EquipRequested;

        private bool Busy
        {
            get
            {
                var panel = GetComponentInParent<ShopPanel>();
                return panel != null && panel.IsBusy;
            }
        }

        private void OnEnable()
        {
            ClientCatalog.Changed += Redraw;
            Game.Content.ItemArt.Changed += Redraw;
            Redraw();
        }

        private void OnDisable()
        {
            ClientCatalog.Changed -= Redraw;
            Game.Content.ItemArt.Changed -= Redraw;
            CloseConfirmation();
            _page = 0;
        }

        public void SetSnapshot(InventorySnapshot snapshot)
        {
            CloseConfirmation();
            _snapshot = snapshot ?? InventorySnapshot.Empty;
            if (!_hasSnapshot) _message = "牌面仅改变你自己看到的效果。";
            _hasSnapshot = true;
            Redraw();
        }

        public void SetFeedback(string message)
        {
            _message = message ?? string.Empty;
            Redraw();
        }

        // Presentation-only snapshot for editor previews and UI verification.
        public void Preview(InventorySnapshot snapshot)
        {
            _snapshot = snapshot ?? InventorySnapshot.Empty;
            _hasSnapshot = true;
            _message = "牌面预览 · 示例库存";
            Redraw();
        }

        public void CancelConfirmation() => CloseConfirmation();
        private void CloseConfirmation()
        {
            _generation++;
            if (_confirmation != null) _confirmation.Hide();
            _confirmation = null;
        }

        private void Select(ItemDefinition item, CardFaceDefinition face, ShopOffer offer, bool owned)
        {
            if (!_hasSnapshot || Busy) return;
            if (owned)
            {
                if (_snapshot.CanEquip && Game.Content.RemoteAssets.IsDownloaded(face.Cards))
                    EquipRequested?.Invoke(face.ItemId);
                return;
            }
            if (!_snapshot.CanAct || item == null || offer == null || !offer.CanPurchase) return;
            CloseConfirmation();
            var generation = _generation;
            var price = offer.Price;
            _confirmation = MsgBox.Create("BuyCardFace");
            _confirmation.Type = MsgboxType.ConfirmAndCancel;
            _confirmation.Title = "收藏牌面";
            _confirmation.Message = $"花费 ${price:N0} 购买「{item.Name}」？";
            _confirmation.ConfirmButtonText = "确认购买";
            _confirmation.CancelButtonText = "取消";
            _confirmation.PreferCancel = true;
            _confirmation.OnConfirm += () =>
            {
                _confirmation = null;
                if (isActiveAndEnabled && generation == _generation && !Busy && ClientCatalog.FindOffer(face.ItemId)?.Price == price)
                    PurchaseRequested?.Invoke(face.ItemId);
            };
            _confirmation.OnCancel += () => _confirmation = null;
        }

        public void Redraw()
        {
            if (_labelTemplate == null || _cardPrefab == null || !isActiveAndEnabled) return;
            foreach (var item in _drawn) if (item != null) { item.SetActive(false); Destroy(item); }
            _drawn.Clear();
            var catalog = ClientCatalog.Current;
            // Owned card faces always show; others only while their shop offer is listed.
            var faces = (catalog?.CardFaces ?? Array.Empty<CardFaceDefinition>()).Where(p => catalog.FindItem(p.ItemId) != null &&
                (_snapshot.Owns(p.ItemId) || catalog.FindOffer(p.ItemId)?.Listed == true)).ToArray();
            var pages = Mathf.Max(1, (faces.Length + PageSize - 1) / PageSize);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            var first = _page * PageSize;
            var equippedId = _snapshot.Equipped(EquipSlot.CardFace);
            var busy = Busy;
            for (var i = first; i < Mathf.Min(first + PageSize, faces.Length); i++)
            {
                var face = faces[i];
                var item = catalog.FindItem(face.ItemId);
                var offer = catalog.FindOffer(face.ItemId);
                var card = Instantiate(_cardPrefab, transform);
                card.name = "CardFace-" + face.ItemId;
                _drawn.Add(card.gameObject);
                var rect = (RectTransform)card.transform;
                rect.anchoredPosition = new Vector2((i - first) % 2 == 0 ? -374 : 374, (i - first) / 2 == 0 ? 143 : -227);
                var owned = _snapshot.Owns(face.ItemId);
                var canAct = _hasSnapshot && !busy && (owned ? _snapshot.CanEquip : _snapshot.CanAct && offer != null && offer.CanPurchase);
                var canAfford = offer != null &&
                    CurrencySystem.CanAfford(offer.Price, _snapshot.Dollars, _snapshot.AvailableChips, _snapshot.CanExchange);
                card.Present(item, face, offer, canAfford, owned, face.ItemId == equippedId, canAct,
                    () => Select(item, face, offer, owned));
            }
            _feedback = Label(_message, transform, new Vector2(0, -515), new Vector2(1160, 40), 21);
            _drawn.Add(_feedback.gameObject);
            var pageLabel = Label($"{_page + 1:00}  /  {pages:00}",
                transform, new Vector2(0, -465), new Vector2(280, 30), 19);
            pageLabel.color = new Color(0.83f, 0.72f, 0.49f);
            var ownedLabel = Label($"已收藏 {faces.Count(p => _snapshot.Owns(p.ItemId))} / {faces.Length}",
                transform, new Vector2(-650, -465), new Vector2(180, 30), 19);
            ownedLabel.color = new Color(0.55f, 0.68f, 0.62f);
            var countLabel = Label(faces.Length == 0 ? "暂无上架牌面" :
                $"第 {first + 1}—{Mathf.Min(first + PageSize, faces.Length)} 款 · 共 {faces.Length} 款",
                transform, new Vector2(650, -465), new Vector2(300, 30), 19);
            countLabel.color = ownedLabel.color;
            _drawn.Add(countLabel.gameObject);
            _drawn.Add(ownedLabel.gameObject);
            _drawn.Add(pageLabel.gameObject);
            var previous = Button("上一页", transform, new Vector2(-200, -465), () => { CloseConfirmation(); _page--; Redraw(); });
            previous.interactable = _page > 0;
            var next = Button("下一页", transform, new Vector2(200, -465), () => { CloseConfirmation(); _page++; Redraw(); });
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
