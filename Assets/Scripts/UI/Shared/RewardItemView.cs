using System.Linq;
using Framework.UI;
using Game.Content;
using Game.Network;
using Game.SO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class RewardItemView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _quantity;
        [SerializeField] private Sprite _dollars;
        [SerializeField] private Sprite _chips;
        [SerializeField] private Sprite _fallback;
        [SerializeField] private RewardTooltipView _tooltipPrefab;
        private readonly RemoteAssetBinding<NamecardArtworkSO> _namecard = new RemoteAssetBinding<NamecardArtworkSO>();
        private RewardTooltipView _tooltip;
        private RewardEntry _item;
        private Reward _bundle;
        private string _name;
        private string _description;
        private Sprite _namecardSprite;

        private void OnEnable()
        {
            _button.onClick.AddListener(ToggleTooltip);
            ClientCatalog.Changed += Refresh;
            ItemArt.Changed += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            _button.onClick.RemoveListener(ToggleTooltip);
            ClientCatalog.Changed -= Refresh;
            ItemArt.Changed -= Refresh;
            _namecard.Dispose();
            _namecardSprite = null;
            CloseTooltip();
        }
        public void Bind(Reward reward)
        {
            Bind(reward?.Entries.Count > 0 ? reward.Entries[0] : null);
            _bundle = reward;
            Refresh();
        }

        public void Bind(RewardEntry item)
        {
            CloseTooltip();
            _namecard.Dispose();
            _namecardSprite = null;
            _bundle = null;
            _item = item;
            Refresh();
        }
        private void Refresh()
        {
            if (_item == null) return;
            _quantity.text = (_item.Kind == RewardItemKind.Item ? "×" : "$") + _item.Quantity.ToString("N0");
            _icon.sprite = _fallback;
            if (_item.Kind != RewardItemKind.Item)
            {
                _name = _item.Kind == RewardItemKind.Dollars ? "美金" : "筹码";
                _description = _item.Kind == RewardItemKind.Dollars ? "可用于商城购买和筹码抽奖。" : "可用于牌局下注，也可兑换为美金。";
                _icon.sprite = _item.Kind == RewardItemKind.Dollars ? _dollars : _chips;
            }
            else
            {
                var item = ClientCatalog.FindItem(_item.ItemId);
                var namecard = ClientCatalog.Current?.Namecards.FirstOrDefault(n => n.ItemId == _item.ItemId);
                var face = ClientCatalog.Current?.CardFaces.FirstOrDefault(c => c.ItemId == _item.ItemId);
                _name = item == null ? "物品" : RewardText.ItemName(_item.ItemId);
                _description = item?.Description ?? "物品信息正在加载。";
                // Every item has an icon; namecard art and card face previews fill in until the icon set is downloaded.
                var icon = ItemArt.ItemIcon(item?.Icon);
                if (icon == null && namecard != null)
                {
                    icon = _namecardSprite;
                    _namecard.Bind(namecard.Artwork, art =>
                    {
                        _namecardSprite = art?.Select(1f);
                        if (ItemArt.ItemIcon(item?.Icon) == null) _icon.sprite = _namecardSprite ?? _fallback;
                    });
                }
                if (icon == null && face != null) icon = ShopLocalArtSO.CardFacePreview(face);
                _icon.sprite = icon ?? _fallback;
            }
            if (_bundle?.Entries.Count > 1)
            {
                _quantity.text = _bundle.Entries.Count + " 项奖励";
                _name = "组合奖励";
                _description = RewardText.Describe(_bundle, "\n");
            }
        }
        private void ToggleTooltip()
        {
            if (_tooltip != null) { CloseTooltip(); return; }
            var panel = GetComponentInParent<BasePanel>();
            if (panel == null || _item == null) return;
            _tooltip = Instantiate(_tooltipPrefab, panel.transform);
            _tooltip.gameObject.SetActive(true);
            _tooltip.Show((RectTransform)transform, _name, _description);
        }
        private void CloseTooltip()
        {
            if (_tooltip != null) { _tooltip.gameObject.SetActive(false); Destroy(_tooltip.gameObject); }
            _tooltip = null;
        }
    }
}
