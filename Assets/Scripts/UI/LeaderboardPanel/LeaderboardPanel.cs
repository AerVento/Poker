using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public enum LeaderboardTab { Wealth, Service }

    public sealed class LeaderboardPanel : SingletonPanel<LeaderboardPanel>
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private Button _back;
        [SerializeField] private Button _refresh;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _list;
        [SerializeField] private LeaderboardRow _rowPrefab;
        [SerializeField] private LeaderboardRow _self;
        [SerializeField] private TextMeshProUGUI _feedback;

        [SerializeField] private Button _wealthTab;
        [SerializeField] private Button _serviceTab;
        [SerializeField] private GameObject _wealthTheme;
        [SerializeField] private GameObject _serviceTheme;
        [SerializeField] private ServiceLeaderboardRow _serviceRowPrefab;
        [SerializeField] private ServiceLeaderboardRow _serviceSelf;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _subtitle;
        [SerializeField] private TextMeshProUGUI _eyebrow;
        [SerializeField] private TextMeshProUGUI _amountHeading;

        public LeaderboardTab SelectedTab { get; private set; }
        private LeaderboardSnapshot _snapshot;
        private bool _hasSnapshot;
        private ulong _requestId;
        private double _deadline;
        private double _refreshAfter;

        private void OnEnable()
        {
            FitContent();
            _hasSnapshot = false;
            _wealthTab.onClick.AddListener(SelectWealth);
            _serviceTab.onClick.AddListener(SelectService);
            SelectTab(LeaderboardTab.Wealth);
            _back.onClick.AddListener(Back);
            _refresh.onClick.AddListener(Refresh);
            _scroll.verticalNormalizedPosition = 1f;
            _refreshAfter = 0;
            Refresh();
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _refresh.onClick.RemoveListener(Refresh);
            _wealthTab.onClick.RemoveListener(SelectWealth);
            _serviceTab.onClick.RemoveListener(SelectService);
            _requestId = 0;
            _hasSnapshot = false;
            _snapshot = default;
        }

        private void OnRectTransformDimensionsChange() => FitContent();

        private void FitContent()
        {
            if (_content == null)
                return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x > 0 && size.y > 0)
                _content.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
        }

        private void Update()
        {
            if (_requestId != 0 && Time.realtimeSinceStartupAsDouble >= _deadline)
            {
                _requestId = 0;
                _feedback.text = "请求超时，请刷新重试。";
            }
            _refresh.interactable = _requestId == 0 && Time.realtimeSinceStartupAsDouble >= _refreshAfter;
        }

        private void Back() => UIManager.Instance.PopPanel<LeaderboardPanel>();

        private void Refresh()
        {
            if (_requestId != 0 || Time.realtimeSinceStartupAsDouble < _refreshAfter)
                return;
            _hasSnapshot = false;
            _snapshot = default;
            ClearRows();
            _self.Show(default);
            _serviceSelf.Show(default);
            _requestId = (NetworkManager.singleton as NetworkRoom)?.RequestLeaderboard() ?? 0;
            _deadline = Time.realtimeSinceStartupAsDouble + 10d;
            _refreshAfter = Time.realtimeSinceStartupAsDouble + 2d;
            _feedback.text = _requestId == 0 ? "请登录后查看排行榜。" : "正在加载排行榜…";
            _refresh.interactable = false;
        }

        private void ClearRows()
        {
            foreach (Transform child in _list)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        public void Receive(LeaderboardResponse response)
        {
            if (!isActiveAndEnabled || _requestId == 0 || response.RequestId != _requestId)
                return;
            _requestId = 0;
            // Start the cooldown after receipt so network latency cannot enable an early retry.
            _refreshAfter = Time.realtimeSinceStartupAsDouble + 2d;
            if (response.Result != LeaderboardResult.Success)
            {
                _feedback.text = response.Result == LeaderboardResult.RateLimited
                    ? "刷新较频繁，请稍后重试。" : "排行榜暂时不可用，请重试。";
                return;
            }
            _snapshot = response.Snapshot;
            _hasSnapshot = true;
            RenderSnapshot();
        }

        private void SelectWealth() => SelectTab(LeaderboardTab.Wealth);
        private void SelectService() => SelectTab(LeaderboardTab.Service);

        public void SelectTab(LeaderboardTab tab)
        {
            if (tab != LeaderboardTab.Wealth && tab != LeaderboardTab.Service)
                return;
            SelectedTab = tab;
            bool service = tab == LeaderboardTab.Service;
            _wealthTab.interactable = service;
            _serviceTab.interactable = !service;
            _wealthTheme.SetActive(!service);
            _serviceTheme.SetActive(service);
            _self.gameObject.SetActive(!service);
            _serviceSelf.gameObject.SetActive(service);
            _title.text = service ? "服役排行榜" : "富豪排行榜";
            _title.color = service ? new Color(1f, 0.78f, 0.36f) : new Color(0.85f, 0.93f, 0.94f);
            _eyebrow.text = service ? "契约档案 · TOP 10" : "财富排行 · TOP 10";
            _subtitle.text = service ? string.Empty : "牌桌风云，尽在此间。";
            _amountHeading.text = service ? "累计服役" : "账户余额";
            if (_hasSnapshot) RenderSnapshot();
            else ClearRows();
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
        }

        private void RenderSnapshot()
        {
            ClearRows();
            if (SelectedTab == LeaderboardTab.Service)
            {
                foreach (var entry in _snapshot.ServiceTopPlayers ?? System.Array.Empty<ServiceLeaderboardEntry>())
                    Instantiate(_serviceRowPrefab, _list).Show(entry);
                _serviceSelf.Show(_snapshot.ServiceSelf);
                _feedback.text = string.Empty;
            }
            else
            {
                foreach (var entry in _snapshot.TopPlayers ?? System.Array.Empty<LeaderboardEntry>())
                    Instantiate(_rowPrefab, _list).Show(entry);
                _self.Show(_snapshot.Self);
                _feedback.text = string.Empty;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_list);
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
