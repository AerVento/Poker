using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
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

        private ulong _requestId;
        private double _deadline;
        private double _refreshAfter;

        private void OnEnable()
        {
            FitContent();
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
            _requestId = 0;
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
            ClearRows();
            _self.Show(default);
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
            foreach (var entry in response.Snapshot.TopPlayers ?? System.Array.Empty<LeaderboardEntry>())
                Instantiate(_rowPrefab, _list).Show(entry);
            _self.Show(response.Snapshot.Self);
            _feedback.text = "按已结算账户余额排名 · 包含离线玩家";
        }
    }
}
