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
        [SerializeField] private TextMeshProUGUI _summary;
        [SerializeField] private TextMeshProUGUI _feedback;
        [SerializeField] private TextMeshProUGUI _empty;
        [SerializeField] private RectTransform _tabs;
        [SerializeField] private Button _tabTemplate;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private AchievementRow _rowPrefab;
        private readonly List<AchievementView> _pages = new List<AchievementView>();
        private readonly List<AchievementRow> _rows = new List<AchievementRow>();
        private readonly List<Button> _tabButtons = new List<Button>();
        private AchievementView[] _views = Array.Empty<AchievementView>();
        private string _category;
        private ulong _statusRequest;
        private ulong _claimRequest;
        private long _revision;
        private int _offset;
        private int _restarts;
        private double _deadline;
        private double _nextPageAt;
        private bool _pagePending;
        private bool _canClaim;
        private bool _dirty;
        private string _claimFeedback;
        private bool Busy => _statusRequest != 0 || _claimRequest != 0 || _pagePending;

        private void OnEnable()
        {
            Fit();
            _back.onClick.AddListener(Back);
            _refresh.onClick.AddListener(Refresh);
            Refresh();
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _refresh.onClick.RemoveListener(Refresh);
            _statusRequest = _claimRequest = 0;
            _pagePending = _dirty = _canClaim = false;
            _claimFeedback = null;
            _views = Array.Empty<AchievementView>();
            _pages.Clear();
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
            if (Busy && Time.realtimeSinceStartupAsDouble >= _deadline)
            {
                _statusRequest = _claimRequest = 0;
                _pagePending = false;
                _canClaim = false;
                _feedback.text = "响应超时，请刷新查看最新状态。";
                DisplayRows();
            }
            if (_pagePending && Time.realtimeSinceStartupAsDouble >= _nextPageAt)
            {
                _pagePending = false;
                RequestPage();
            }
            if (_dirty && !Busy) Refresh();
            _refresh.interactable = !Busy;
        }

        public void Refresh()
        {
            if (Busy) return;
            _dirty = false;
            _pages.Clear();
            _offset = _restarts = 0;
            _revision = 0;
            _canClaim = false;
            _feedback.text = "正在读取成就…";
            RequestPage();
            DisplayRows();
        }

        private void RequestPage()
        {
            _statusRequest = (NetworkManager.singleton as NetworkRoom)?.RequestAchievements(_offset, _revision) ?? 0;
            _deadline = Time.realtimeSinceStartupAsDouble + 10;
            if (_statusRequest == 0) _feedback.text = "请先登录，再查看成就。";
        }

        public void ReceiveStatus(AchievementsResponse response)
        {
            if (!isActiveAndEnabled || _statusRequest == 0 || response.RequestId != _statusRequest) return;
            _statusRequest = 0;
            if (response.Result == AchievementResult.RevisionChanged && _restarts++ < 2)
            {
                _pages.Clear(); _offset = 0; _revision = 0;
                SchedulePage();
                return;
            }
            if (response.Result != AchievementResult.Success || response.Achievements == null || response.Offset != _offset)
            {
                _feedback.text = ResultMessage(response.Result);
                DisplayRows();
                return;
            }
            _revision = response.Revision;
            _pages.AddRange(response.Achievements);
            if (response.NextOffset > _offset)
            {
                _offset = response.NextOffset;
                SchedulePage();
                return;
            }
            Present(_pages.ToArray(), response.CanClaim);
            _feedback.text = _claimFeedback ?? (response.CanClaim ? "每个成就仅可完成一次。" : "成就进度实时保存，奖励请回到大厅领取。");
            _claimFeedback = null;
        }

        private void SchedulePage()
        {
            _pagePending = true;
            _nextPageAt = Time.realtimeSinceStartupAsDouble + 0.15;
            _deadline = Time.realtimeSinceStartupAsDouble + 10;
        }

        public void Present(AchievementView[] views, bool canClaim)
        {
            _views = views;
            _canClaim = canClaim;
            _summary.text = $"已达成  {_views.Count(v => v.CompletedAt > 0)} / {_views.Length}";
            BuildTabs();
            DisplayRows();
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
            foreach (var view in _views.Where(v => _category == null || v.Category == _category))
            {
                var row = Instantiate(_rowPrefab, _scroll.content);
                row.gameObject.SetActive(true);
                row.Bind(view, _canClaim, Busy, Claim);
                _rows.Add(row);
            }
            _empty.gameObject.SetActive(_rows.Count == 0);
            _empty.text = _views.Length == 0 ? "暂无成就数据" : "这个类别暂时没有成就";
            _scroll.verticalNormalizedPosition = 1;
        }

        private void Claim(string id)
        {
            if (Busy || !_canClaim) return;
            _claimRequest = (NetworkManager.singleton as NetworkRoom)?.RequestAchievementClaim(id) ?? 0;
            _deadline = Time.realtimeSinceStartupAsDouble + 10;
            if (_claimRequest == 0) _feedback.text = "请回到大厅领取奖励。";
            DisplayRows();
        }

        public void ReceiveClaim(AchievementClaimResponse response)
        {
            if (!isActiveAndEnabled || _claimRequest == 0 || response.RequestId != _claimRequest) return;
            _claimRequest = 0;
            Refresh();
            _claimFeedback = response.Result == AchievementResult.Success
                ? (response.GrantedChips > 0 ? $"领取成功，${response.GrantedChips:N0} 筹码已到账。" : "领取成功。") +
                    (response.GrantedItems != null && response.GrantedItems.Length > 0 ? " 物品已加入背包。" : "")
                : ResultMessage(response.Result);
            _feedback.text = _claimFeedback;
        }

        public void AchievementChanged() { if (isActiveAndEnabled) _dirty = true; }

        private static string ResultMessage(AchievementResult result) => result switch
        {
            AchievementResult.AlreadyClaimed => "这个成就的奖励已经领取。",
            AchievementResult.NotComplete => "尚未达成这个成就。",
            AchievementResult.OutsideRoomRequired => "请回到大厅领取奖励。",
            AchievementResult.BalanceLimit => "筹码余额已达上限，奖励仍保留待领取。",
            AchievementResult.ItemLimit => "奖励物品数量已达上限，奖励仍保留待领取。",
            AchievementResult.RateLimited => "操作过于频繁，请稍后刷新。",
            AchievementResult.RevisionChanged => "成就状态发生变化，请刷新。",
            _ => "暂时无法读取成就，请刷新重试。"
        };
    }
}
