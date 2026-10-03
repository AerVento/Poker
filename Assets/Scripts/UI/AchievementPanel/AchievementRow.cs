using System;
using System.Linq;
using Game.Achievements;
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
        [SerializeField] private RectTransform _rewards;
        [SerializeField] private ScrollRect _rewardScroll;
        [SerializeField] private RewardItemView _rewardPrefab;
        private readonly System.Collections.Generic.List<RewardItemView> _rewardViews = new System.Collections.Generic.List<RewardItemView>();
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
            var hidden = view.Hidden && view.CompletedAt == 0;
            _icon.sprite = LoadIcon(hidden ? HiddenAchievementPresentation.IconPath : view.IconPath);
            _icon.color = view.CompletedAt > 0 ? Color.white : new Color(0.65f, 0.67f, 0.70f);
            _name.text = hidden ? HiddenAchievementPresentation.Name : view.Name;
            _description.text = hidden ? HiddenAchievementPresentation.Description : view.Description;
            _description.enableWordWrapping = true;
            _description.richText = false;
            _expanded = false;
            _bound = true;
            _category.text = view.Category;
            _progress.text = hidden ? "?? / ??" : view.CompletedAt > 0 ? "已达成" : $"{view.Progress:N0} / {view.Target:N0}";
            _progressFill.fillAmount = hidden ? 0 : view.Target > 0 ? Mathf.Clamp01((float)view.Progress / view.Target) : 0;
            _accent.color = view.CompletedAt > 0 ? new Color(0.87f, 0.70f, 0.39f) : new Color(0.23f, 0.34f, 0.31f);
            BindRewards(view.Reward, hidden);
            RefreshDescriptionLayout();
            _claimLabel.text = view.ClaimedAt > 0 ? "已领取" : hidden ? "未解锁" :
                view.CompletedAt == 0 ? "未达成" : !canClaim ? "暂不可领" : "领取奖励";
            _claim.interactable = !busy && canClaim && view.CompletedAt > 0 && view.ClaimedAt == 0;
            _claim.onClick.RemoveAllListeners();
            _claim.onClick.AddListener(() => claim(view.Id));
        }

        public static Sprite LoadIcon(string path) => Resources.Load<Sprite>(path ?? "") ?? Resources.Load<Sprite>("UI/Achievements/default");

        private void BindRewards(Reward reward, bool hidden)
        {
            var entries = (reward ?? Reward.Empty).Entries;
            int count = hidden ? 0 : entries.Count;
            _reward.gameObject.SetActive(hidden);
            _rewardScroll.gameObject.SetActive(!hidden && count > 0);
            _reward.text = "??";
            for (int i = 0; i < count; i++)
            {
                if (i == _rewardViews.Count) _rewardViews.Add(Instantiate(_rewardPrefab, _rewards));
                _rewardViews[i].gameObject.SetActive(true);
                _rewardViews[i].Bind(entries[i]);
            }
            for (int i = count; i < _rewardViews.Count; i++) _rewardViews[i].gameObject.SetActive(false);
            _rewardScroll.StopMovement();
            _rewards.anchoredPosition = Vector2.zero;
        }
    }
}
