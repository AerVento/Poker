using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class DailyTaskPanel : SingletonPanel<DailyTaskPanel>
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private Button _back;
        [SerializeField] private Button _refresh;
        [SerializeField] private TextMeshProUGUI _balance;
        [SerializeField] private TextMeshProUGUI _resetTime;
        [SerializeField] private TextMeshProUGUI _feedback;
        [SerializeField] private DailyTaskCard[] _cards;

        private DailyTasksSnapshot _snapshot;
        private ulong _statusRequest;
        private ulong _claimRequest;
        private bool _hasSnapshot;
        private double _requestDeadline;
        private double _resetAt;
        private bool IsBusy => _statusRequest != 0 || _claimRequest != 0;

        private void OnEnable()
        {
            FitContent();
            _back.onClick.AddListener(Back);
            _refresh.onClick.AddListener(Refresh);
            foreach (var card in _cards)
                card.ClaimClicked += Claim;
            _feedback.text = string.Empty;
            _balance.text = "账户余额  ·  —";
            _resetTime.text = "每日 00:00 刷新 · UTC+8";
            Refresh();
        }

        private void OnRectTransformDimensionsChange() => FitContent();

        private void FitContent()
        {
            if (_content == null)
                return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0 || size.y <= 0)
                return;
            _content.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _refresh.onClick.RemoveListener(Refresh);
            foreach (var card in _cards)
                card.ClaimClicked -= Claim;
            _statusRequest = 0;
            _claimRequest = 0;
            _hasSnapshot = false;
            _snapshot = default;
        }

        private void Update()
        {
            if (IsBusy && Time.realtimeSinceStartupAsDouble >= _requestDeadline)
            {
                _statusRequest = 0;
                _claimRequest = 0;
                _hasSnapshot = false;
                _feedback.text = "响应超时，请刷新查看最新状态；已到账的奖励不会重复发放。";
                foreach (var card in _cards)
                    card.DisableClaim();
            }
            if (_hasSnapshot && !IsBusy && Time.realtimeSinceStartupAsDouble >= _resetAt)
                Refresh();
            _refresh.interactable = !IsBusy;
        }

        private void Back() => UIManager.Instance.PopPanel<DailyTaskPanel>();

        public void Refresh()
        {
            if (IsBusy)
                return;
            _hasSnapshot = false;
            _feedback.text = string.Empty;
            foreach (var card in _cards)
                card.SetLoading();
            _statusRequest = (NetworkManager.singleton as NetworkRoom)?.RequestDailyTasks() ?? 0;
            _requestDeadline = Time.realtimeSinceStartupAsDouble + 10;
            if (_statusRequest == 0)
                ShowUnavailable("请先登录并离开房间，再查看每日任务。");
        }

        private void Claim(DailyTaskId taskId)
        {
            if (IsBusy || !_hasSnapshot)
                return;
            if (Time.realtimeSinceStartupAsDouble >= _resetAt)
            {
                Refresh();
                return;
            }
            foreach (var state in _snapshot.Tasks)
            {
                if (state.Id != taskId || state.Availability != DailyTaskAvailability.Claimable)
                    continue;
                _claimRequest = (NetworkManager.singleton as NetworkRoom)?.RequestDailyTaskClaim(taskId, _snapshot.Period) ?? 0;
                _requestDeadline = Time.realtimeSinceStartupAsDouble + 10;
                if (_claimRequest == 0)
                    ShowUnavailable("请先登录并离开房间，再领取奖励。");
                else
                    DisplayCards();
                return;
            }
        }

        public void ReceiveStatus(DailyTasksResponse response)
        {
            if (!isActiveAndEnabled || _statusRequest == 0 || response.RequestId != _statusRequest)
                return;
            _statusRequest = 0;
            if (response.Result != DailyTaskResult.Success || response.Snapshot.Tasks == null)
            {
                ShowUnavailable(ResultMessage(response.Result));
                return;
            }
            _snapshot = response.Snapshot;
            _hasSnapshot = true;
            _resetAt = Time.realtimeSinceStartupAsDouble +
                System.Math.Max(1, _snapshot.NextResetUnixSeconds - _snapshot.ServerUnixSeconds);
            _balance.text = $"账户余额  ·  ${_snapshot.Balance:N0}";
            _resetTime.text = "每日 00:00 刷新 · UTC+8";
            GameController.Instance.State.SetChips(_snapshot.Balance);
            DisplayCards();
        }

        public void ReceiveClaim(DailyTaskClaimResponse response)
        {
            if (!isActiveAndEnabled || _claimRequest == 0 || response.RequestId != _claimRequest)
                return;
            _claimRequest = 0;
            Refresh();
            _feedback.text = response.Result == DailyTaskResult.Success
                ? $"领取成功，${response.GrantedAmount:N0} 已到账。" : ResultMessage(response.Result);
        }

        private void DisplayCards()
        {
            foreach (var card in _cards)
            {
                foreach (var state in _snapshot.Tasks)
                {
                    if (state.Id == card.TaskId)
                        card.Display(state, IsBusy);
                }
            }
        }

        private void ShowUnavailable(string message)
        {
            _hasSnapshot = false;
            _feedback.text = message;
            foreach (var card in _cards)
                card.DisableClaim();
        }

        private static string ResultMessage(DailyTaskResult result) => result switch
        {
            DailyTaskResult.AlreadyClaimed => "该任务今日已经领取过了。",
            DailyTaskResult.BalanceTooHigh => "当前余额高于 $1,000，无法领取应急筹码。",
            DailyTaskResult.ActiveHand => "账号尚有未结算的手牌，无法领取奖励。",
            DailyTaskResult.OutsideRoomRequired => "请离开房间后再领取奖励。",
            DailyTaskResult.PeriodChanged => "已进入新的一天，任务状态正在刷新，请重新领取。",
            DailyTaskResult.BalanceLimit => "账户余额已达上限，无法领取奖励。",
            _ => "暂时无法读取任务，请刷新后重试。",
        };
    }
}
