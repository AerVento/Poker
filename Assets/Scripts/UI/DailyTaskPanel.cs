using Framework.UI;
using System.Collections.Generic;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed partial class DailyTaskPanel : SingletonPanel<DailyTaskPanel>
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
        private ulong _deleteRequest;
        private MsgBox _deleteConfirmation;
        private bool _hasSnapshot;
        private double _requestDeadline;
        private double _resetAt;
        private bool IsBusy => _statusRequest != 0 || _claimRequest != 0 || _deleteRequest != 0;
        private readonly List<DailyTaskState> _pages = new List<DailyTaskState>();
        private int _pageOffset;
        private long _pageRevision;
        private int _pageRestarts;

        private void OnEnable()
        {
            EnsureScrollView();
            FitContent();
            _back.onClick.AddListener(Back);
            _refresh.onClick.AddListener(Refresh);
            foreach (var card in _cards)
            {
                card.InstanceClaimClicked += Claim;
                card.DeleteClicked += ConfirmDelete;
            }
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
            {
                card.InstanceClaimClicked -= Claim;
                card.DeleteClicked -= ConfirmDelete;
            }
            CancelDeleteConfirmation();
            _statusRequest = 0;
            _claimRequest = 0;
            _deleteRequest = 0;
            _hasSnapshot = false;
            _snapshot = default;
        }

        private void Update()
        {
            if (IsBusy && Time.realtimeSinceStartupAsDouble >= _requestDeadline)
            {
                _statusRequest = 0;
                _claimRequest = 0;
                _deleteRequest = 0;
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
            CancelDeleteConfirmation();
            _hasSnapshot = false;
            _feedback.text = string.Empty;
            _pages.Clear();
            _pageOffset = 0;
            _pageRevision = 0;
            _pageRestarts = 0;
            foreach (var card in _cards)
                card.SetLoading();
            _statusRequest = (NetworkManager.singleton as NetworkRoom)?.RequestDailyTasks() ?? 0;
            _requestDeadline = Time.realtimeSinceStartupAsDouble + 10;
            if (_statusRequest == 0)
                ShowUnavailable("请先登录并离开房间，再查看每日任务。");
        }

        private void Claim(string instanceId, int ordinal)
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
                if (state.InstanceId != instanceId || state.Availability != DailyTaskAvailability.Claimable)
                    continue;
                _claimRequest = (NetworkManager.singleton as NetworkRoom)?.RequestDailyTaskClaim(instanceId, ordinal) ?? 0;
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
            if (response.Result == DailyTaskResult.Success && response.Snapshot.Tasks == null && _pageRestarts++ < 2)
            {
                _pages.Clear();
                _pageOffset = 0;
                _pageRevision = 0;
                RequestPage();
                return;
            }
            if (response.Result != DailyTaskResult.Success || response.Snapshot.Tasks == null)
            {
                ShowUnavailable(ResultMessage(response.Result));
                return;
            }
            if (response.Snapshot.Offset != _pageOffset || (_pageOffset > 0 && response.Snapshot.Revision != _pageRevision))
            {
                ShowUnavailable("任务状态已变化，请刷新。");
                return;
            }
            _pages.AddRange(response.Snapshot.Tasks);
            _pageRevision = response.Snapshot.Revision;
            if (response.Snapshot.NextOffset > _pageOffset)
            {
                _pageOffset = response.Snapshot.NextOffset;
                RequestPage();
                return;
            }
            _snapshot = response.Snapshot;
            _snapshot.Tasks = _pages.ToArray();
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
                ? $"领取成功，${response.GrantedAmount:N0} 已到账。" +
                    (response.GrantedItems != null && response.GrantedItems.Length > 0 ? " 道具已加入背包。" : "") : ResultMessage(response.Result);
        }

        private void DisplayCards()
        {
            EnsureScrollView();
            var selected = _scroll.CurrentIndex;
            var retained = System.Array.FindIndex(_snapshot.Tasks, t => t.InstanceId == _visibleInstanceId);
            if (retained >= 0) selected = retained;
            EnsureCardCount(_snapshot.Tasks.Length);
            for (var i = 0; i < _cards.Length; i++)
            {
                _cards[i].gameObject.SetActive(i < _snapshot.Tasks.Length);
                if (i < _snapshot.Tasks.Length) _cards[i].Display(_snapshot.Tasks[i], IsBusy);
            }
            _scroll.SetPages(_snapshot.Tasks.Length, 704, selected);
            if (_snapshot.Tasks.Length == 0) _feedback.text = "当前没有任务。新一天登录时会补充随机任务。";
        }

        private void ConfirmDelete(string instanceId)
        {
            if (IsBusy || !_hasSnapshot || !UIManager.Available) return;
            var state = System.Array.Find(_snapshot.Tasks, t => t.InstanceId == instanceId);
            if (!state.CanDelete) return;
            CancelDeleteConfirmation();
            _deleteConfirmation = MsgBox.Create("DeleteDailyTask");
            _deleteConfirmation.Type = MsgboxType.ConfirmAndCancel;
            _deleteConfirmation.Title = "删除随机任务";
            _deleteConfirmation.Message = $"删除「{state.Name}」？\n当前可领取的奖励将自动到账，剩余进度和轮次将放弃。\n当天不补发，下一业务日补充空位。";
            _deleteConfirmation.ConfirmButtonText = "确认删除";
            _deleteConfirmation.CancelButtonText = "保留任务";
            _deleteConfirmation.PreferCancel = true;
            _deleteConfirmation.OnCancel += () => _deleteConfirmation = null;
            _deleteConfirmation.OnConfirm += () =>
            {
                _deleteConfirmation = null;
                if (!isActiveAndEnabled || IsBusy || !_hasSnapshot) return;
                if (Time.realtimeSinceStartupAsDouble >= _resetAt) { Refresh(); return; }
                if (!System.Array.Exists(_snapshot.Tasks, t => t.InstanceId == instanceId && t.CanDelete)) return;
                _deleteRequest = (NetworkManager.singleton as NetworkRoom)?.RequestDailyTaskDelete(instanceId) ?? 0;
                _requestDeadline = Time.realtimeSinceStartupAsDouble + 10;
                if (_deleteRequest == 0) ShowUnavailable("连接不可用，请重新登录。");
                else DisplayCards();
            };
        }

        private void CancelDeleteConfirmation()
        {
            var confirmation = _deleteConfirmation;
            _deleteConfirmation = null;
            if (confirmation != null) confirmation.Hide();
        }

        public void ReceiveDelete(DailyTaskDeleteResponse response)
        {
            if (!isActiveAndEnabled || _deleteRequest == 0 || response.RequestId != _deleteRequest) return;
            _deleteRequest = 0;
            Refresh();
            _feedback.text = response.Result == DailyTaskResult.Success
                ? "任务已删除，空位将在下一业务日补充。" +
                    (response.GrantedAmount > 0 ? $" ${response.GrantedAmount:N0} 奖励已自动到账。" : "") +
                    (response.GrantedItems != null && response.GrantedItems.Length > 0 ? " 奖励道具已加入背包。" : "")
                : ResultMessage(response.Result);
        }

        private void RequestPage()
        {
            _statusRequest = (NetworkManager.singleton as NetworkRoom)?.RequestDailyTasksPage(_pageOffset, _pageRevision) ?? 0;
            _requestDeadline = Time.realtimeSinceStartupAsDouble + 10;
            if (_statusRequest == 0) ShowUnavailable("连接不可用，请重新登录。");
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
            DailyTaskResult.AlreadyClaimed => "这一轮奖励已经领取过了。",
            DailyTaskResult.AlreadyDeleted => "任务已经删除，空位将在下一业务日补充。",
            DailyTaskResult.FixedTask => "固定任务不能删除。",
            DailyTaskResult.BalanceTooHigh => "当前余额尚未满足任务的领取条件。",
            DailyTaskResult.NotComplete => "本轮目标尚未完成。",
            DailyTaskResult.Expired => "任务已到期，未完成的轮次不再开放。",
            DailyTaskResult.ItemLimit => "奖励道具已达持有上限，暂时无法领取。",
            DailyTaskResult.ActiveHand => "账号尚有未结算的手牌，无法领取奖励。",
            DailyTaskResult.OutsideRoomRequired => "请离开房间后再领取奖励。",
            DailyTaskResult.PeriodChanged => "已进入新的一天，任务状态正在刷新，请重新领取。",
            DailyTaskResult.BalanceLimit => "账户余额已达上限，无法领取奖励。",
            _ => "暂时无法读取任务，请刷新后重试。",
        };
    }
}
