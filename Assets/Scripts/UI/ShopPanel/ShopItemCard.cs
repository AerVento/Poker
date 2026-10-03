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

        private NamecardDefinition _card;
        private string _itemId;
        private Action<string> _onAction;
        private bool _canSelect;
        private readonly RemoteAssetBinding<NamecardArtworkSO> _binding = new RemoteAssetBinding<NamecardArtworkSO>();

        private void OnEnable()
        {
            _action.onClick.AddListener(Act);
            if (_card != null) BindArtwork();
        }

        private void OnDisable()
        {
            _action.onClick.RemoveListener(Act);
            _onAction = null;
            _artwork.sprite = null;
            _binding.Dispose();
        }

        /// <summary>Shows a namecard item: its name and description, its art, and its shop offer when it has one.</summary>
        public void Present(ItemDefinition item, NamecardDefinition card, ShopOffer offer, string playerName, uint dollars,
            bool canAfford, bool hasAccount, bool owned, bool equipped, bool canAct, Action<string> onAction)
        {
            _card = card;
            _itemId = item?.Id ?? card?.ItemId;
            _onAction = onAction;
            BindArtwork();
            var purchasable = offer != null && offer.CanPurchase;
            var isDefault = card != null && card.IsDefault;
            _name.text = item?.Name ?? _itemId;
            _description.text = item?.Description ?? string.Empty;
            _price.text = isDefault ? "免费默认款" : owned ? "永久拥有" : !purchasable ? "" : $"${offer.Price:N0}";
            _status.text = equipped ? "当前装备" : isDefault ? "默认拥有" : owned ? "已收藏" : "永久名片";
            _playerName.text = string.IsNullOrEmpty(playerName) ? "Poker Player" : playerName;
            _playerChips.text = hasAccount ? "$" + ChipAmountFormatter.Format(dollars) : "POKER";
            _canSelect = owned || purchasable;
            _actionLabel.text = equipped ? "已装备" : owned ? "装备" :
                !purchasable ? "不可购买" :
                hasAccount && !canAfford ? "美金不足" : "购买";
            _action.interactable = _canSelect && canAct && hasAccount && !equipped && (owned || canAfford);
            _border.color = equipped ? new Color(0.76f, 0.65f, 0.40f, 0.85f) : new Color(0.26f, 0.42f, 0.37f, 0.52f);
        }

        private void BindArtwork()
        {
            if (!isActiveAndEnabled || _card == null) return;
            _binding.Bind(_card.Artwork, artwork =>
            {
                // The default namecard's surface is local; the others are remote artwork.
                var sprite = artwork != null ? artwork.Wide ?? artwork.Compact :
                    _card.IsDefault && ShopLocalArtSO.Instance != null ? ShopLocalArtSO.Instance.DefaultNamecard : null;
                _artwork.sprite = sprite;
                _artwork.type = _card.IsDefault ? Image.Type.Sliced : Image.Type.Simple;
                _artwork.preserveAspect = !_card.IsDefault;
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
            if (_action.interactable && _itemId != null && _canSelect)
                _onAction?.Invoke(_itemId);
        }
    }
}
