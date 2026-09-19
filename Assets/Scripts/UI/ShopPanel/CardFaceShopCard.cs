using System;
using Game.SO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class CardFaceShopCard : MonoBehaviour
    {
        [SerializeField] private RectTransform _preview;
        [SerializeField] private Image _border;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _price;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private Button _action;
        [SerializeField] private TextMeshProUGUI _actionLabel;
        private Action _onAction;

        private void OnEnable() => _action.onClick.AddListener(Act);
        private void OnDisable() { _action.onClick.RemoveListener(Act); _onAction = null; }
        private void Act() { if (_action.interactable) _onAction?.Invoke(); }

        public void Present(CardFaceProductSO product, uint price, bool canAfford, bool owned, bool equipped,
            bool canAct, Action onAction)
        {
            _name.text = product.DisplayName;
            _description.text = product.Description;
            _price.text = CardFaceItemId.IsDefaultOwned(product.Id) ? "免费默认款" : owned ? "永久拥有" : $"${price:N0}";
            _status.text = equipped ? "当前装备" : owned ? "已收藏" : "永久牌面";
            _actionLabel.text = equipped ? "已装备" : owned ? "装备" : !canAfford ? "美金不足" : "购买";
            _action.interactable = canAct && !equipped && (owned || canAfford);
            _border.color = equipped ? new Color(0.76f, 0.65f, 0.40f, 0.85f) : new Color(0.26f, 0.42f, 0.37f, 0.52f);
            _onAction = onAction;
            AddPreview(product, -89, true);
            AddPreview(product, 89, false);
        }

        private void AddPreview(CardFaceProductSO product, float x, bool front)
        {
            var view = Instantiate(CardFaceAppearance.Catalog.ViewPrefab, _preview);
            var rect = (RectTransform)view.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0);
            rect.sizeDelta = new Vector2(145, 205);
            view.SetCards(product.Cards);
            view.Display(new Poker(PokerSuit.Spade, PokerRank.Ace), front);
        }
    }
}
