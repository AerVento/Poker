using System;
using Game.SO;
using Game.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class CardFaceShopCard : MonoBehaviour
    {
        [SerializeField] private RectTransform _preview;
        [SerializeField] private Image _border;
        [SerializeField] private Image _previewImage;
        [SerializeField] private ResourceDownloadView _downloadView;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _price;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private Button _action;
        [SerializeField] private TextMeshProUGUI _actionLabel;
        private Action _onAction;
        private CardFaceDefinition _product;
        private bool _owned;
        private bool _equipped;
        private bool _canAct;
        private bool _canAfford;
        private Color _actionColor;
        private Color _labelColor;
        private ColorBlock _actionColors;

        private void Awake()
        {
            _actionColor = _action.image.color;
            _labelColor = _actionLabel.color;
            _actionColors = _action.colors;
        }

        private void OnEnable() => _action.onClick.AddListener(Act);
        private void OnDisable()
        {
            _action.onClick.RemoveListener(Act);
            _onAction = null;
        }
        private void Update() => RefreshAction();

        private void Act() { if (_action.interactable) _onAction?.Invoke(); }

        public void Present(CardFaceDefinition product, uint price, bool canAfford, bool owned, bool equipped,
            bool canAct, Action onAction)
        {
            _name.text = product.Name;
            _description.text = product.Description;
            _price.text = CardFaceItemId.IsDefaultOwned(product.Id) ? "免费默认款" : owned ? "永久拥有" : $"${price:N0}";
            _status.text = equipped ? "当前装备" : owned ? "已收藏" : "永久牌面";
            _border.color = equipped ? new Color(0.76f, 0.65f, 0.40f, 0.85f) : new Color(0.26f, 0.42f, 0.37f, 0.52f);
            _onAction = onAction;
            _product = product;
            _owned = owned;
            _equipped = equipped;
            _canAct = canAct;
            _canAfford = canAfford;
            // The classic preview is local; other previews come from the shared remote set and appear when it loads.
            var preview = ShopLocalArtSO.CardFacePreview(product);
            _previewImage.sprite = preview;
            _previewImage.enabled = preview != null;
            _downloadView.Bind(product.Cards, true);
            RefreshAction();
        }

        private void RefreshAction()
        {
            if (_product == null) return;
            var downloaded = RemoteAssets.IsDownloaded(_product.Cards);
            var unavailable = _owned && !downloaded;
            var colors = _actionColors;
            if (unavailable) colors.disabledColor = new Color(0.30f, 0.32f, 0.32f, 1f);
            _action.colors = colors;
            _action.image.color = unavailable ? Color.white : _actionColor;
            _actionLabel.color = unavailable ? new Color32(164, 185, 173, 255) : _labelColor;
            _actionLabel.text = _owned && !downloaded ? "未下载" : _equipped ? "已装备" :
                _owned ? "装备" : !_canAfford ? "美金不足" : "购买";
            _action.interactable = _canAct && !_equipped && (_owned ? downloaded : _canAfford);
            _status.text = RemoteAssets.DownloadState(_product.Cards) == RemoteDownloadState.Failed
                ? "下载失败，请重试" : _equipped ? "当前装备" : _owned ? "已收藏" : "永久牌面";
        }
    }
}
