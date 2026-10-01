using System;
using Game.Network.Accounts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class DailyTaskCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _reward;
        [SerializeField] private Image _rewardCurrencyIcon;
        [SerializeField] private TextMeshProUGUI _progress;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private Button _claim;
        [SerializeField] private TextMeshProUGUI _claimLabel;

        public event Action<string, int> InstanceClaimClicked;
        public event Action<string> DeleteClicked;
        private Button _delete;
        private DailyTaskState _state;
        private RectTransform _objectiveRows;
        private RectTransform _objectiveContent;
        private TextMeshProUGUI _roundLabel;
        private Image _roundFill;
        private readonly System.Collections.Generic.List<TextMeshProUGUI> _objectiveLabels = new System.Collections.Generic.List<TextMeshProUGUI>();
        private readonly System.Collections.Generic.List<Image> _objectiveFills = new System.Collections.Generic.List<Image>();

        private void OnEnable()
        {
            _claim.onClick.AddListener(Claim);
            if (_delete != null) { _delete.onClick.RemoveListener(Delete); _delete.onClick.AddListener(Delete); }
        }
        private void OnDisable()
        {
            _claim.onClick.RemoveListener(Claim);
            if (_delete != null) _delete.onClick.RemoveListener(Delete);
        }
        private void Delete() => DeleteClicked?.Invoke(_state.InstanceId);
        private void Claim()
        {
            InstanceClaimClicked?.Invoke(_state.InstanceId, _state.ClaimOrdinal);
        }

        public void SetLoading()
        {
            if (_delete != null) _delete.interactable = false;
            SetClaimButton("读取中…", false);
            _progress.text = "— / 1";
            _status.text = "正在读取今日任务状态";
        }

        public void Display(DailyTaskState state, bool busy, bool canClaim = true)
        {
            _state = state;
            _title.text = state.Name;
            _description.text = state.Description;
            _reward.text = $"+ ${state.Reward:N0}" + (state.RewardItems != null && state.RewardItems.Length > 0 ? " + 道具" : "");
            if (_rewardCurrencyIcon != null) _rewardCurrencyIcon.gameObject.SetActive(state.Reward > 0);
            DisplayProgress(state);
            EnsureDeleteButton();
            _delete.GetComponentInChildren<TaskActionIcon>().Display(state.CanRefresh);
            _delete.gameObject.SetActive(state.CanDelete || state.CanRefresh);
            _delete.interactable = !busy && canClaim && state.CanDelete;
            _title.rectTransform.sizeDelta = new Vector2(state.CanDelete ? 480 : 544, 60);
            _title.rectTransform.anchoredPosition = new Vector2(state.CanDelete ? -32 : 0, 199);
            _status.text = state.Availability switch
            {
                DailyTaskAvailability.Claimable => state.IsDirectClaim ? "" : "本轮已完成",
                DailyTaskAvailability.Claimed => "全部奖励已领取",
                DailyTaskAvailability.InProgress => string.Empty,
                DailyTaskAvailability.Expired => "已到期",
                DailyTaskAvailability.OutsideRoom => string.Empty,
                DailyTaskAvailability.ItemLimit => "奖励道具已达持有上限",
                DailyTaskAvailability.BalanceTooHigh => "当前总资产尚未满足领取条件",
                DailyTaskAvailability.ActiveHand => string.Empty,
                _ => "美金余额已达上限，暂不可领取",
            };
            if (state.OutDated) _status.text = "历史任务 · 奖励领取后自动移除";
            var label = busy ? "处理中…" : !canClaim ? "暂不可领取" : state.Availability switch
            {
                DailyTaskAvailability.Claimable => "领取奖励",
                DailyTaskAvailability.Claimed => "已全部领取",
                _ => "暂不可领取",
            };
            SetClaimButton(label, !busy && canClaim && state.Availability == DailyTaskAvailability.Claimable);
        }

        private void DisplayProgress(DailyTaskState state)
        {
            // Rows are recreated from serialized skin references after cloning a template card.
            if (_objectiveRows == null)
            {
                var old = transform.Find("ObjectiveRows");
                if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
                _objectiveRows = (RectTransform)new GameObject("ObjectiveRows", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(TaskObjectiveScrollRect)).transform;
                _objectiveRows.SetParent(transform, false);
                _objectiveRows.sizeDelta = new Vector2(544, 142);
                _objectiveRows.anchoredPosition = new Vector2(0, -20);
                _objectiveRows.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);
                _objectiveContent = (RectTransform)new GameObject("Rows", typeof(RectTransform)).transform;
                _objectiveContent.SetParent(_objectiveRows, false);
                _objectiveContent.anchorMin = new Vector2(0, 1);
                _objectiveContent.anchorMax = Vector2.one;
                _objectiveContent.pivot = new Vector2(0.5f, 1);
                var scroll = _objectiveRows.GetComponent<ScrollRect>();
                scroll.viewport = _objectiveRows;
                scroll.content = _objectiveContent;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 30;
                _progress.gameObject.SetActive(false);
                var number = transform.Find("Number");
                if (number != null) number.gameObject.SetActive(false);
                _title.enableAutoSizing = true;
                _title.fontSizeMin = 20;
                _title.fontSizeMax = 40;
                _title.overflowMode = TextOverflowModes.Ellipsis;
                _description.fontSize = 20;
                _description.overflowMode = TextOverflowModes.Ellipsis;
                _description.rectTransform.anchoredPosition = new Vector2(0, 82);
                _description.rectTransform.sizeDelta = new Vector2(544, 64);
                _reward.fontSize = 32;
                var rewardRect = _reward.transform.parent.name == "RewardCurrencyDisplay"
                    ? (RectTransform)_reward.transform.parent : _reward.rectTransform;
                rewardRect.anchoredPosition = new Vector2(-40, 132);
                rewardRect.sizeDelta = new Vector2(464, 40);
                _title.rectTransform.anchoredPosition = new Vector2(0, 199);
                _status.fontSize = 18;
                _status.rectTransform.anchoredPosition = new Vector2(0, -145);
                _status.rectTransform.sizeDelta = new Vector2(544, 28);
                var divider = transform.Find("Divider");
                if (divider != null) divider.gameObject.SetActive(false);
                var unit = transform.Find("Unit");
                if (unit != null) unit.gameObject.SetActive(false);
                var previousRound = transform.Find("总完成次数");
                if (previousRound != null) { previousRound.gameObject.SetActive(false); Destroy(previousRound.gameObject); }
                _roundFill = MakeRow("总完成次数", out _roundLabel, transform);
            }
            var count = state.Objectives?.Length ?? 0;
            while (_objectiveLabels.Count < count)
            {
                _objectiveFills.Add(MakeRow("目标", out var label));
                _objectiveLabels.Add(label);
            }
            _objectiveContent.sizeDelta = new Vector2(0, Mathf.Max(142, count * 44));
            _objectiveRows.gameObject.SetActive(count > 0);
            for (var i = 0; i < _objectiveLabels.Count; i++)
            {
                _objectiveLabels[i].transform.parent.gameObject.SetActive(i < count);
                if (i >= count) continue;
                var objective = state.Objectives[i];
                SetRow(_objectiveLabels[i], _objectiveFills[i], $"{objective.Description}   {objective.Progress}/{objective.Target}",
                    objective.Target == 0 ? 0 : (float)objective.Progress / objective.Target, -18 - i * 44);
            }
            SetRow(_roundLabel, _roundFill, $"总完成次数   {state.CompletedCount}/{state.MaxCompletions}",
                state.MaxCompletions == 0 ? 0 : (float)state.CompletedCount / state.MaxCompletions, -111);
            if (state.ExpiresAtUnixSeconds > 0)
            {
                var end = DateTimeOffset.FromUnixTimeSeconds(state.ExpiresAtUnixSeconds).ToOffset(TimeSpan.FromHours(8));
                _status.text = "";
                _description.text = state.Description + $"\n{end:MM-dd HH:mm} 到期";
                if (state.IsDirectClaim) _description.text = state.Description + $"\n{end:MM-dd HH:mm} 到期";
            }
        }

        private Image MakeRow(string name, out TextMeshProUGUI label, Transform parent = null)
        {
            var row = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            row.SetParent(parent != null ? parent : _objectiveContent, false);
            if (parent == null) row.anchorMin = row.anchorMax = new Vector2(0.5f, 1);
            row.sizeDelta = new Vector2(544, 40);
            label = Instantiate(_progress, row);
            label.gameObject.SetActive(true);
            label.fontSize = 17;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = 17;
            label.alignment = TextAlignmentOptions.Left;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(0, 4);
            label.rectTransform.sizeDelta = new Vector2(544, 24);
            label.raycastTarget = false;
            var background = new GameObject("Track", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(row, false);
            background.rectTransform.sizeDelta = new Vector2(544, 5);
            background.rectTransform.anchoredPosition = new Vector2(0, -12);
            background.color = new Color(0.2f, 0.3f, 0.26f);
            background.raycastTarget = false;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fill.transform.SetParent(background.transform, false);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.sizeDelta = Vector2.zero;
            fill.color = new Color(0.45f, 0.8f, 0.57f);
            fill.raycastTarget = false;
            return fill;
        }

        private void EnsureDeleteButton()
        {
            if (_delete != null) return;
            var existing = transform.Find("DeleteButton");
            if (existing != null) _delete = existing.GetComponent<Button>();
            else
            {
                _delete = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
                _delete.transform.SetParent(transform, false);
                var rect = (RectTransform)_delete.transform;
                rect.sizeDelta = new Vector2(44, 44);
                rect.anchoredPosition = new Vector2(224, 203);
                _delete.targetGraphic = _delete.GetComponent<Image>();
                var background = _delete.GetComponent<Image>();
                background.sprite = (_claim.targetGraphic as Image)?.sprite;
                background.type = Image.Type.Sliced;
                background.color = new Color(0.12f, 0.23f, 0.18f, 1);
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(TaskActionIcon)).GetComponent<TaskActionIcon>();
                icon.transform.SetParent(_delete.transform, false);
                icon.rectTransform.sizeDelta = new Vector2(30, 30);
                icon.color = new Color(0.94f, 0.82f, 0.63f);
                icon.raycastTarget = false;
            }
            _delete.onClick.RemoveAllListeners();
            _delete.onClick.AddListener(Delete);
        }

        private static void SetRow(TextMeshProUGUI label, Image fill, string text, float progress, float y)
        {
            label.text = text;
            ((RectTransform)label.transform.parent).anchoredPosition = new Vector2(0, y);
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
        }

        private void SetClaimButton(string label, bool interactable)
        {
            _claim.interactable = interactable;
            _claimLabel.text = label;
            _claimLabel.color = interactable ? new Color(0.07f, 0.11f, 0.09f, 1f) : new Color(0.70f, 0.77f, 0.73f, 1f);
        }

        public void DisableClaim()
        {
            if (_delete != null) _delete.interactable = false;
            SetClaimButton("请刷新状态", false);
        }
    }
}
