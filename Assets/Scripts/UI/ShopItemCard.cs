using System;
using Game.SO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class ShopItemCard : MonoBehaviour
    {
        [SerializeField] private Image _artwork;
        [SerializeField] private Image _border;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _price;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private TextMeshProUGUI _playerName;
        [SerializeField] private TextMeshProUGUI _playerChips;
        [SerializeField] private Button _action;
        [SerializeField] private TextMeshProUGUI _actionLabel;

        private NamecardProductSO _product;
        private Action<NamecardProductSO> _onAction;

        private void OnEnable() => _action.onClick.AddListener(Act);

        private void OnDisable()
        {
            _action.onClick.RemoveListener(Act);
            _onAction = null;
        }

        public void Present(NamecardProductSO product, string playerName, uint balance,
            bool hasAccount, bool owned, bool equipped, bool canAct, Action<NamecardProductSO> onAction, uint? quotedPrice = null)
        {
            var price = quotedPrice ?? product.Price;
            _product = product;
            _onAction = onAction;
            _artwork.sprite = product.Artwork;
            _artwork.type = product.IsDefault ? Image.Type.Sliced : Image.Type.Simple;
            _artwork.preserveAspect = !product.IsDefault;
            _artwork.enabled = product.Artwork != null;
            _name.text = product.DisplayName;
            _description.text = product.Description;
            _price.text = product.IsDefault ? "免费默认款" : owned && product.CanPurchase ? "永久拥有" : $"${price:N0}";
            _status.text = equipped ? "当前装备" : product.IsDefault ? "默认拥有" : owned ? "已收藏" : "永久名片";
            _playerName.text = string.IsNullOrEmpty(playerName) ? "Poker Player" : playerName;
            _playerChips.text = hasAccount ? "$" + ChipAmountFormatter.Format(balance) : "POKER";
            _actionLabel.text = !product.CanPurchase ? "不可购买" : equipped ? "已装备" : owned ? "装备" :
                hasAccount && balance < price ? "余额不足" : "购买";
            _action.interactable = product.CanPurchase && canAct && hasAccount && !equipped && (owned || balance >= price);
            _border.color = equipped ? new Color(0.76f, 0.65f, 0.40f, 0.85f) : new Color(0.26f, 0.42f, 0.37f, 0.52f);
        }

        private void Act()
        {
            if (_action.interactable && _product != null && _product.CanPurchase)
                _onAction?.Invoke(_product);
        }
    }
}
