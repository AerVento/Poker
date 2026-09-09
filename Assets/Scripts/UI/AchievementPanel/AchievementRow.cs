using System;
using System.Linq;
using Game.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class AchievementRow : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _accent;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _category;
        [SerializeField] private TextMeshProUGUI _progress;
        [SerializeField] private Image _progressFill;
        [SerializeField] private TextMeshProUGUI _reward;
        [SerializeField] private Button _claim;
        [SerializeField] private TextMeshProUGUI _claimLabel;

        [SerializeField] private Button _descriptionToggle;
        [SerializeField] private TextMeshProUGUI _descriptionToggleLabel;
        [SerializeField] private RectTransform _progressTrack;
        private LayoutElement _layoutElement;
        private float _collapsedHeight;
        private float _collapsedDescriptionHeight;
        private Vector2 _trackPosition;
        private Vector2 _fillPosition;
        private Vector2 _progressPosition;
        private Vector2 _togglePosition;
        private float _descriptionWidth = -1;
        private bool _expanded;
        private bool _descriptionLayoutDirty;
        private bool _bound;

        private void OnEnable()
        {
            _descriptionToggle?.onClick.AddListener(ToggleDescription);
            _descriptionLayoutDirty = true;
        }

        private void OnDisable()
        {
            _descriptionToggle?.onClick.RemoveListener(ToggleDescription);
        }

        private void LateUpdate()
        {
            if (_bound && (_descriptionLayoutDirty || !Mathf.Approximately(_descriptionWidth, _description.rectTransform.rect.width)))
            {
                RefreshDescriptionLayout();
            }
        }

        private void CacheLayout()
        {
            if (_layoutElement != null) return;
            _layoutElement = GetComponent<LayoutElement>();
            _collapsedHeight = _layoutElement.preferredHeight;
            _collapsedDescriptionHeight = _description.rectTransform.sizeDelta.y;
            _trackPosition = _progressTrack.anchoredPosition;
            _fillPosition = _progressFill.rectTransform.anchoredPosition;
            _progressPosition = _progress.rectTransform.anchoredPosition;
            _togglePosition = ((RectTransform)_descriptionToggle.transform).anchoredPosition;
        }

        private void ToggleDescription()
        {
            _expanded = !_expanded;
            RefreshDescriptionLayout();
        }

        private void RefreshDescriptionLayout()
        {
            CacheLayout();
            _descriptionWidth = _description.rectTransform.rect.width;
            if (_descriptionWidth <= 0) return;
            _descriptionLayoutDirty = false;
            var fullHeight = Mathf.Ceil(_description.GetPreferredValues(_description.text, _descriptionWidth, Mathf.Infinity).y);
            var truncated = fullHeight > _collapsedDescriptionHeight + 0.5f;
            if (!truncated) _expanded = false;
            var extra = _expanded ? Mathf.Max(0, fullHeight - _collapsedDescriptionHeight) : 0;
            _description.overflowMode = _expanded ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            _description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _collapsedDescriptionHeight + extra);
            _layoutElement.preferredHeight = _collapsedHeight + extra;
            _progressTrack.anchoredPosition = _trackPosition + Vector2.down * extra;
            _progressFill.rectTransform.anchoredPosition = _fillPosition + Vector2.down * extra;
            _progress.rectTransform.anchoredPosition = _progressPosition + Vector2.down * extra;
            ((RectTransform)_descriptionToggle.transform).anchoredPosition = _togglePosition + Vector2.down * extra;
            _descriptionToggleLabel.text = _expanded ? "收起 −" : "详情 +";
            _descriptionToggle.gameObject.SetActive(truncated);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        public void Bind(AchievementView view, bool canClaim, bool busy, Action<string> claim)
        {
            _icon.sprite = LoadIcon(view.IconPath);
            _icon.color = view.CompletedAt > 0 ? Color.white : new Color(0.65f, 0.67f, 0.70f);
            _name.text = view.Name;
            _description.text = view.Description;
            _description.enableWordWrapping = true;
            _description.richText = false;
            _expanded = false;
            _bound = true;
            RefreshDescriptionLayout();
            _category.text = view.Category;
            _progress.text = view.CompletedAt > 0 ? "已达成" : $"{view.Progress:N0} / {view.Target:N0}";
            _progressFill.fillAmount = view.Target > 0 ? Mathf.Clamp01((float)view.Progress / view.Target) : 0;
            _accent.color = view.CompletedAt > 0 ? new Color(0.87f, 0.70f, 0.39f) : new Color(0.23f, 0.34f, 0.31f);
            var rewards = new System.Collections.Generic.List<string>();
            if (view.RewardChips > 0) rewards.Add($"${view.RewardChips:N0} 筹码");
            if (view.RewardItems != null) rewards.AddRange(view.RewardItems.Select(i => ItemName(i.ItemId) + " ×" + i.Count));
            _reward.text = string.Join("\n", rewards);
            _claimLabel.text = view.ClaimedAt > 0 ? "已领取" : view.CompletedAt == 0 ? "未达成" : !canClaim ? "大厅领取" : "领取奖励";
            _claim.interactable = !busy && canClaim && view.CompletedAt > 0 && view.ClaimedAt == 0;
            _claim.onClick.RemoveAllListeners();
            _claim.onClick.AddListener(() => claim(view.Id));
        }

        public static Sprite LoadIcon(string path) => Resources.Load<Sprite>(path ?? "") ?? Resources.Load<Sprite>("UI/Achievements/Default");

        private static string ItemName(string id)
        {
            if (NamecardItemId.TryToNamecardId(id, out var namecardId))
            {
                var namecard = Resources.Load<Game.SO.NamecardCatalogSO>("SO/Shop/NamecardCatalog")?.GetProduct(namecardId);
                return namecard != null ? namecard.DisplayName + "名片" : id;
            }
            var item = Resources.LoadAll<Game.SO.ItemDefinitionSO>("SO/Items").FirstOrDefault(i => i.Id == id);
            return item != null ? item.DisplayName : id;
        }
    }
}
