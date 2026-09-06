using System;
using Game.Network.Accounts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class DailyTaskCard : MonoBehaviour
    {
        [SerializeField] private DailyTaskId _taskId;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _reward;
        [SerializeField] private TextMeshProUGUI _progress;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private Button _claim;
        [SerializeField] private TextMeshProUGUI _claimLabel;

        public DailyTaskId TaskId => _taskId;
        public event Action<DailyTaskId> ClaimClicked;

        private void OnEnable() => _claim.onClick.AddListener(Claim);
        private void OnDisable() => _claim.onClick.RemoveListener(Claim);
        private void Claim() => ClaimClicked?.Invoke(_taskId);

        public void SetLoading()
        {
            SetClaimButton("读取中…", false);
            _progress.text = "— / 1";
            _status.text = "正在读取今日任务状态";
        }

        public void Display(DailyTaskState state, bool busy)
        {
            _title.text = state.Name;
            _description.text = state.Description;
            _reward.text = $"+ {state.Reward:N0}";
            _progress.text = state.Availability == DailyTaskAvailability.Claimed ? "1 / 1" : "0 / 1";
            _status.text = state.Availability switch
            {
                DailyTaskAvailability.Claimable => "今日可领取",
                DailyTaskAvailability.Claimed => "今日奖励已到账，明日再来",
                DailyTaskAvailability.BalanceTooHigh => "当前余额尚未满足领取条件",
                DailyTaskAvailability.ActiveHand => "尚有未结算的手牌，暂不可领取",
                _ => "余额已达上限，暂不可领取",
            };
            var label = busy ? "处理中…" : state.Availability switch
            {
                DailyTaskAvailability.Claimable => "领取奖励",
                DailyTaskAvailability.Claimed => "今日已领取",
                _ => "暂不可领取",
            };
            SetClaimButton(label, !busy && state.Availability == DailyTaskAvailability.Claimable);
        }

        private void SetClaimButton(string label, bool interactable)
        {
            _claim.interactable = interactable;
            _claimLabel.text = label;
            _claimLabel.color = interactable ? new Color(0.07f, 0.11f, 0.09f, 1f) : new Color(0.70f, 0.77f, 0.73f, 1f);
        }

        public void DisableClaim()
        {
            SetClaimButton("请刷新状态", false);
        }
    }
}
