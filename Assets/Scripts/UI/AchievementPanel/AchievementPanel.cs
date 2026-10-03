using System;
using System.Collections.Generic;
using System.Linq;
using Framework.UI;
using Game.Achievements;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class AchievementPanel : SingletonPanel<AchievementPanel>
    {
        [SerializeField] private RectTransform _layout;
        [SerializeField] private Button _back;
        [SerializeField] private Button _refresh;
        [SerializeField] private Button _claimAll;
        [SerializeField] private TextMeshProUGUI _claimAllLabel;
        [SerializeField] private TextMeshProUGUI _summary;
        [SerializeField] private TextMeshProUGUI _feedback;
        [SerializeField] private TextMeshProUGUI _empty;
        [SerializeField] private RectTransform _tabs;
        [SerializeField] private Button _tabTemplate;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private AchievementRow _rowPrefab;
        private readonly List<AchievementRow> _rows = new List<AchievementRow>();
        private readonly List<Button> _tabButtons = new List<Button>();
        private AchievementView[] _views = Array.Empty<AchievementView>();
        private string _category;
        private ulong _statusRequest;
        private ulong _claimRequest;
        private NetworkRoom _network;
        private bool _canClaim;
        private string _claimFeedback;
        private bool Busy => (_network != null && (_network.AchievementCacheBusy || _network.PendingAchievementClaim != 0)) || _claimRequest != 0;

        private void OnEnable()
        {
            Fit();
            _back.onClick.AddListener(Back);
            _refresh.onClick.AddListener(Refresh);
            _claimAll?.onClick.AddListener(ClaimAll);
            _network = NetworkManager.singleton as NetworkRoom;
            if (_network != null)
            {
                _network.AchievementCacheChanged += ReceiveCache;
                _network.AchievementClaimReceived += ReceiveClaim;
                _network.AchievementBatchClaimReceived += ReceiveBatchClaim;
                _network.OpenCachedAchievements();
            }
            ReceiveCache();
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _refresh.onClick.RemoveListener(Refresh);
            _claimAll?.onClick.RemoveListener(ClaimAll);
            if (_network != null)
            {
                _network.AchievementCacheChanged -= ReceiveCache;
                _network.AchievementClaimReceived -= ReceiveClaim;
                _network.AchievementBatchClaimReceived -= ReceiveBatchClaim;
                _network.CloseCachedAchievements();
            }
            _network = null;
            _statusRequest = _claimRequest = 0;
            _canClaim = false;
            _claimFeedback = null;
            _views = Array.Empty<AchievementView>();
            ClearRows();
        }

        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (_layout == null) return;
            var rect = ((RectTransform)transform).rect;
            if (rect.width > 0 && rect.height > 0) _layout.localScale = Vector3.one * Mathf.Min(rect.width / 1920f, rect.height / 1080f);
        }
        private void Back() => UIManager.Instance.PopPanel<AchievementPanel>();

        private void Update()
        {
            if (_network != null)
            {
                if (_claimRequest != 0 && _network.PendingAchievementClaim != _claimRequest)
                {
                    _claimRequest = 0;
                    ReceiveCache();
                }
                var canClaim = _network.CanClaimCachedAchievements;
                if (_canClaim != canClaim)
                {
                    _canClaim = canClaim;
                    DisplayRows();
                }
            }
            _refresh.interactable = !Busy;
            UpdateClaimAll();
        }

        public void Refresh()
        {
            if (Busy) return;
            _claimFeedback = null;
            _network?.OpenCachedAchievements(forceStateRefresh: true);
            ReceiveCache();
        }

        private void ReceiveCache()
        {
            if (!isActiveAndEnabled) return;
            // Kept as a busy marker for existing diagnostics; pagination belongs to NetworkRoom.
            _statusRequest = _network != null && _network.AchievementCacheBusy ? 1UL : 0UL;
            if (_network != null && _network.TryGetCachedAchievements(out var views))
                Present(views, _network.CanClaimCachedAchievements);
            else
                Present(Array.Empty<AchievementView>(), false);
            _feedback.text = _claimFeedback ?? _network?.AchievementCacheError ??
                (_network == null || !_network.CanViewAchievements ? "请先登录，再查看成就。" :
                    _network.AchievementCacheBusy ? "正在读取成就…" :
                    _network.AchievementProgressDirty ? "进度有更新。" :
                    "");
            _refresh.interactable = !Busy;
            UpdateClaimAll();
        }

        public void Present(AchievementView[] views, bool canClaim)
        {
            var keepScroll = _views.Length > 0;
            var scrollPosition = _scroll.verticalNormalizedPosition;
            _views = views;
            _canClaim = canClaim;
            _summary.text = $"已达成  {_views.Count(v => v.CompletedAt > 0)} / {_views.Length}";
            BuildTabs();
            DisplayRows();
            if (keepScroll) _scroll.verticalNormalizedPosition = scrollPosition;
        }

        private void BuildTabs()
        {
            foreach (var button in _tabButtons) ReleaseView(button.gameObject);
            _tabButtons.Clear();
            var categories = new List<string> { null, AchievementCategories.General, AchievementCategories.Special };
            categories.AddRange(_views.Select(v => v.Category).Where(c => !categories.Contains(c)).Distinct().OrderBy(c => c));
            if (!categories.Contains(_category)) _category = null;
            foreach (var category in categories)
            {
                var button = Instantiate(_tabTemplate, _tabs);
                button.name = "Category_" + (category ?? "All");
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<TextMeshProUGUI>().text = (category ?? "全部") + "  " + _views.Count(v => category == null || v.Category == category);
                button.onClick.AddListener(() => SelectCategory(category));
                _tabButtons.Add(button);
            }
            UpdateTabs();
        }

        public void SelectCategory(string category)
        {
            _category = category;
            UpdateTabs();
            DisplayRows();
        }

        private void UpdateTabs()
        {
            foreach (var button in _tabButtons)
            {
                var selected = button.name == "Category_" + (_category ?? "All");
                button.GetComponent<Image>().color = selected ? new Color(0.79f, 0.63f, 0.35f) : new Color(0.10f, 0.20f, 0.17f);
                button.GetComponentInChildren<TextMeshProUGUI>().color = selected ? new Color(0.07f, 0.12f, 0.10f) : new Color(0.78f, 0.85f, 0.81f);
            }
        }

        private static void ReleaseView(GameObject view)
        {
            view.SetActive(false);
            if (Application.isPlaying) Destroy(view);
            else DestroyImmediate(view);
        }

        private void ClearRows()
        {
            foreach (var row in _rows) ReleaseView(row.gameObject);
            _rows.Clear();
        }

        private void DisplayRows()
        {
            ClearRows();
            var ordered = _views.Select((view, index) => new { View = view, Index = index })
                .Where(item => _category == null || item.View.Category == _category)
                .OrderBy(item => SortPriority(item.View))
                .ThenBy(item => item.Index)
                .Select(item => item.View);
            foreach (var view in ordered)
            {
                var row = Instantiate(_rowPrefab, _scroll.content);
                row.gameObject.SetActive(true);
                row.Bind(view, _canClaim, Busy, Claim);
                _rows.Add(row);
            }
            _empty.gameObject.SetActive(_rows.Count == 0);
            _empty.text = _views.Length == 0 ? "暂无成就数据" : "这个类别暂时没有成就";
            _scroll.verticalNormalizedPosition = 1;
            UpdateClaimAll();
        }

        private static int SortPriority(AchievementView view)
        {
            if (view.CompletedAt > 0) return view.ClaimedAt == 0 ? 0 : 1;
            return view.Hidden ? 3 : 2;
        }

        private void UpdateClaimAll()
        {
            if (_claimAll == null) return;
            var count = _views.Count(view => (_category == null || view.Category == _category) &&
                view.CompletedAt > 0 && view.ClaimedAt == 0);
            _claimAll.interactable = !Busy && _canClaim && count > 0;
            if (_claimAllLabel != null) _claimAllLabel.text = count > 0 ? $"一键领取  {count}" : "一键领取";
        }

        private void Claim(string id)
        {
            if (Busy || !_canClaim) return;
            _claimRequest = (NetworkManager.singleton as NetworkRoom)?.RequestAchievementClaim(id) ?? 0;
            if (_claimRequest == 0) _feedback.text = "暂时无法领取奖励。";
            DisplayRows();
        }

        private void ClaimAll()
        {
            if (Busy || !_canClaim) return;
            _claimRequest = _network?.RequestAchievementBatchClaim(_category) ?? 0;
            if (_claimRequest == 0) _feedback.text = "暂时无法领取奖励。";
            DisplayRows();
        }

        public void ReceiveClaim(AchievementClaimResponse response)
        {
            if (!isActiveAndEnabled || _claimRequest == 0 || response.RequestId != _claimRequest) return;
            _claimRequest = 0;
            if (response.Result == AchievementResult.Success) ShowClaimRewards(response.GrantedReward);
            _claimFeedback = response.Result == AchievementResult.Success
                ? "奖励已领取"
                : ResultMessage(response.Result);
            ReceiveCache();
        }

        public void ReceiveBatchClaim(AchievementBatchClaimResponse response)
        {
            if (!isActiveAndEnabled || _claimRequest == 0 || response.RequestId != _claimRequest) return;
            _claimRequest = 0;
            if (response.Result == AchievementResult.Success) ShowClaimRewards(response.GrantedReward);
            _claimFeedback = response.Result == AchievementResult.Success
                ? $"已领取 {response.ClaimedCount} 项奖励"
                : response.Result == AchievementResult.AlreadyClaimed || response.Result == AchievementResult.NotComplete
                    ? "当前页签没有可领取奖励。"
                : ResultMessage(response.Result);
            ReceiveCache();
        }

        private static void ShowClaimRewards(Reward reward)
        {
            if (reward?.Entries.Count > 0) UIManager.Instance.PushPanel<RewardPopupPanel>().Present(reward.Entries);
        }

        private static string ResultMessage(AchievementResult result) => result switch
        {
            AchievementResult.AlreadyClaimed => "这个成就的奖励已经领取。",
            AchievementResult.NotComplete => "尚未达成这个成就。",
            AchievementResult.OutsideRoomRequired => "当前无法领取奖励。",
            AchievementResult.BalanceLimit => "货币余额已达上限，奖励仍保留待领取。",
            AchievementResult.ItemLimit => "奖励物品暂不可用，请稍后重试。",
            AchievementResult.RateLimited => "操作过于频繁，请稍后刷新。",
            AchievementResult.RevisionChanged => "成就状态发生变化，请刷新。",
            _ => "暂时无法读取成就，请刷新重试。"
        };
    }
}
