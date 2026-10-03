using System.Collections.Generic;
using Framework.UI;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class SlotPrizePoolPanel : SingletonPanel<SlotPrizePoolPanel>
    {
        [SerializeField] private RectTransform _window;
        [SerializeField] private RectTransform _items;
        [SerializeField] private SlotPoolRewardView _rowPrefab;
        [SerializeField] private Button _close;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _chance;
        [SerializeField] private TextMeshProUGUI _page;
        [SerializeField] private TextMeshProUGUI _status;
        private readonly List<SlotPoolRewardView> _rows = new List<SlotPoolRewardView>();
        private NetworkRoom _network;
        private SlotPrizeTier _tier;
        private ulong _request;
        private int _offset;
        private int _total;
        private double _retryAt;

        private void OnEnable()
        {
            _network = NetworkManager.singleton as NetworkRoom;
            if (_network != null) _network.SlotLotteryReceived += Receive;
            _close.onClick.AddListener(Close);
            _previous.onClick.AddListener(Previous);
            _next.onClick.AddListener(Next);
            Fit();
        }
        private void OnDisable()
        {
            if (_network != null) _network.SlotLotteryReceived -= Receive;
            _network = null;
            _request = 0;
            _close.onClick.RemoveListener(Close);
            _previous.onClick.RemoveListener(Previous);
            _next.onClick.RemoveListener(Next);
            ClearRows();
        }
        public void Present(SlotPrizeTier tier)
        {
            _tier = tier;
            _offset = 0;
            _total = 0;
            _title.text = SlotProbabilityText.TierName(tier) + "奖池";
            _chance.text = "";
            _page.text = "";
            ClearRows();
            Request();
        }
        private void Request()
        {
            _request = _network?.RequestSlotLottery(SlotLotteryAction.Pool, tier: _tier, offset: _offset) ?? 0;
            _retryAt = Time.realtimeSinceStartupAsDouble + 8;
            _status.text = _request == 0 ? "暂时无法读取奖池" : "加载中…";
            _previous.interactable = _next.interactable = false;
        }
        private void Update()
        {
            if (_request != 0 && Time.realtimeSinceStartupAsDouble >= _retryAt) Request();
        }
        private void Receive(SlotLotteryResponse response)
        {
            if (!isActiveAndEnabled || _request == 0 || response.RequestId != _request) return;
            if (response.Result == SlotLotteryResult.RateLimited)
            {
                _retryAt = Time.realtimeSinceStartupAsDouble + 0.3;
                return;
            }
            _request = 0;
            var disclosure = response.Disclosure;
            if (response.Result != SlotLotteryResult.Success || disclosure == null || disclosure.Tier != _tier)
            {
                _status.text = "暂时无法读取奖池";
                return;
            }
            ClearRows();
            _offset = disclosure.Offset;
            _total = disclosure.TotalRewards;
            _chance.text = "单次中奖概率 " + SlotProbabilityText.Format(disclosure.Probability(_tier));
            _status.text = _total == 0 ? "暂无奖励" : "";
            foreach (var reward in disclosure.Rewards)
            {
                var row = Instantiate(_rowPrefab, _items);
                row.gameObject.SetActive(true);
                row.Present(reward, disclosure);
                _rows.Add(row);
            }
            _page.text = $"{_offset / SlotLotteryDisclosure.PageSize + 1} / {Mathf.Max(1, (_total + SlotLotteryDisclosure.PageSize - 1) / SlotLotteryDisclosure.PageSize)}";
            _previous.interactable = _offset > 0;
            _next.interactable = _offset + SlotLotteryDisclosure.PageSize < _total;
        }
        private void ClearRows()
        {
            foreach (var row in _rows) if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            _rows.Clear();
        }
        private void Previous() { if (_request != 0 || _offset == 0) return; _offset -= SlotLotteryDisclosure.PageSize; Request(); }
        private void Next() { if (_request != 0 || _offset + SlotLotteryDisclosure.PageSize >= _total) return; _offset += SlotLotteryDisclosure.PageSize; Request(); }
        private void Close() => UIManager.Instance.PopPanel<SlotPrizePoolPanel>();
        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (_window == null) return;
            var size = ((RectTransform)transform).rect.size;
            _window.localScale = Vector3.one * Mathf.Min(1f, size.x / 1200f, size.y / 850f);
        }
    }
}
