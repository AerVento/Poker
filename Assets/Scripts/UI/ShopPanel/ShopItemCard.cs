using System;
using Game.SO;
using Game.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class ShopItemCard : MonoBehaviour
    {
        [SerializeField] private Image _artwork;
        [SerializeField] private Image _border;
        [SerializeField] private ArtworkLoadingView _loadingView;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _price;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private TextMeshProUGUI _playerName;
        [SerializeField] private TextMeshProUGUI _playerChips;
        [SerializeField] private Button _action;
        [SerializeField] private TextMeshProUGUI _actionLabel;

        private NamecardDefinition _product;
        private Action<NamecardDefinition> _onAction;
        private bool _canSelect;
        private readonly RemoteAssetBinding<NamecardArtworkSO> _binding = new RemoteAssetBinding<NamecardArtworkSO>();

        private void OnEnable()
        {
            _action.onClick.AddListener(Act);
            if (_product != null) BindArtwork();
        }

        private void OnDisable()
        {
            _action.onClick.RemoveListener(Act);
            _onAction = null;
            _artwork.sprite = null;
            _binding.Dispose();
        }

        public void Present(NamecardDefinition product, string playerName, uint dollars, bool canAfford,
            bool hasAccount, bool owned, bool equipped, bool canAct, Action<NamecardDefinition> onAction, uint? quotedPrice = null)
        {
            var price = quotedPrice ?? product.Price;
            _product = product;
            _onAction = onAction;
            BindArtwork();
            _name.text = product.Name;
            _description.text = product.Description;
            _price.text = product.IsDefault ? "免费默认款" : owned ? "永久拥有" : product.Reward ? "奖励名片" : $"${price:N0}";
            _status.text = equipped ? "当前装备" : product.IsDefault ? "默认拥有" : owned ? "已收藏" : "永久名片";
            _playerName.text = string.IsNullOrEmpty(playerName) ? "Poker Player" : playerName;
            _playerChips.text = hasAccount ? "$" + ChipAmountFormatter.Format(dollars) : "POKER";
            _canSelect = owned || product.CanPurchase;
            _actionLabel.text = equipped ? "已装备" : owned ? "装备" :
                !product.CanPurchase ? "不可购买" :
                hasAccount && !canAfford ? "美金不足" : "购买";
            _action.interactable = _canSelect && canAct && hasAccount && !equipped && (owned || canAfford);
            _border.color = equipped ? new Color(0.76f, 0.65f, 0.40f, 0.85f) : new Color(0.26f, 0.42f, 0.37f, 0.52f);
        }

        public void Present(NamecardDefinition product, string playerName, uint dollars,
            bool hasAccount, bool owned, bool equipped, bool canAct, Action<NamecardDefinition> onAction,
            uint? quotedPrice = null)
        {
            Present(product, playerName, dollars, dollars >= (quotedPrice ?? product.Price), hasAccount,
                owned, equipped, canAct, onAction, quotedPrice);
        }

        private void BindArtwork()
        {
            if (!isActiveAndEnabled) return;
            _binding.Bind(_product.Artwork, artwork =>
            {
                // The default namecard's surface is local; the others are remote artwork.
                var sprite = artwork != null ? artwork.Wide ?? artwork.Compact :
                    _product.IsDefault && ShopLocalArtSO.Instance != null ? ShopLocalArtSO.Instance.DefaultNamecard : null;
                _artwork.sprite = sprite;
                _artwork.type = _product.IsDefault ? Image.Type.Sliced : Image.Type.Simple;
                _artwork.preserveAspect = !_product.IsDefault;
                _artwork.enabled = sprite != null;
            }, state =>
            {
                _loadingView?.Present(state);
                _playerName.gameObject.SetActive(state == RemoteAssetState.Ready);
                _playerChips.gameObject.SetActive(state == RemoteAssetState.Ready);
            });
        }

        private void Act()
        {
            if (_action.interactable && _product != null && _canSelect)
                _onAction?.Invoke(_product);
        }
    }
}
