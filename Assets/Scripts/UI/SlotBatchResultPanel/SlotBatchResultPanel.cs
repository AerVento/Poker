using System;
using System.Collections.Generic;
using System.Linq;
using Framework.UI;
using Game.Network.Accounts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class SlotBatchResultPanel : SingletonPanel<SlotBatchResultPanel>
    {
        [SerializeField] private RectTransform _window;
        [SerializeField] private RectTransform _items;
        [SerializeField] private SlotBatchResultView _itemPrefab;
        [SerializeField] private Button _close;
        [SerializeField] private Button _return;
        [SerializeField] private Button _confirm;
        [SerializeField] private Button _details;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private ScrollRect _summaryScroll;
        [SerializeField] private RewardItemView _rewardPrefab;
        [SerializeField] private TextMeshProUGUI _empty;
        [SerializeField] private TextMeshProUGUI _title;
        private readonly List<SlotBatchResultView> _views = new List<SlotBatchResultView>();
        private readonly List<RewardItemView> _summaryViews = new List<RewardItemView>();
        private Action _closed;
        private bool _showingDetails;

        private void OnEnable()
        {
            _close.onClick.AddListener(Close);
            _return.onClick.AddListener(ShowSummary);
            _confirm.onClick.AddListener(Close);
            _details.onClick.AddListener(ShowDetails);
            Fit();
        }

        private void OnDisable()
        {
            _close.onClick.RemoveListener(Close);
            _return.onClick.RemoveListener(ShowSummary);
            _confirm.onClick.RemoveListener(Close);
            _details.onClick.RemoveListener(ShowDetails);
            _closed = null;
            Clear();
        }

        public void Present(SlotDrawOutcome[] results, Action closed)
        {
            Clear();
            _closed = closed;
            _scroll.StopMovement();
            _items.anchoredPosition = Vector2.zero;
            _summaryScroll.StopMovement();
            _summaryScroll.content.anchoredPosition = Vector2.zero;
            for (int i = 0; i < results.Length; i++)
            {
                var view = Instantiate(_itemPrefab, _items);
                view.gameObject.SetActive(true);
                view.Present(results[i], i);
                _views.Add(view);
            }
            var total = new Reward(results.SelectMany(result => (result.Reward ?? Reward.Empty).Entries));
            foreach (var item in total.Entries)
            {
                var view = Instantiate(_rewardPrefab, _summaryScroll.content);
                view.gameObject.SetActive(true);
                view.Bind(item);
                _summaryViews.Add(view);
            }
            _empty.text = results.Any(result => result.Tier != SlotPrizeTier.None) ? "奖励已达持有上限" : "本次未中奖";
            ShowSummary();
        }

        private void ShowSummary() => SetDetails(false);
        private void ShowDetails() => SetDetails(true);

        private void SetDetails(bool details)
        {
            _showingDetails = details;
            _window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, details ? 960f : 520f);
            _window.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                details ? 1240f : Mathf.Clamp(_summaryViews.Count * 152f + 96f, 800f, 1240f));
            float width = _window.rect.width - 120f;
            ((RectTransform)_summaryScroll.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _summaryScroll.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _summaryScroll.content.GetComponent<LayoutElement>().minWidth = width;
            _scroll.verticalScrollbar.gameObject.SetActive(details);
            _title.text = details ? "抽奖详情" : "本次获得";
            _scroll.gameObject.SetActive(details);
            _summaryScroll.gameObject.SetActive(!details && _summaryViews.Count > 0);
            _empty.gameObject.SetActive(!details && _summaryViews.Count == 0);
            _return.gameObject.SetActive(details);
            _confirm.gameObject.SetActive(!details);
            _details.gameObject.SetActive(!details);
        }

        private void Clear()
        {
            foreach (var view in _views)
                if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            foreach (var view in _summaryViews)
                if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            _views.Clear();
            _summaryViews.Clear();
        }

        private void Close()
        {
            if (_showingDetails) { ShowSummary(); return; }
            var closed = _closed;
            _closed = null;
            UIManager.Instance.PopPanel<SlotBatchResultPanel>();
            closed?.Invoke();
        }

        private void OnRectTransformDimensionsChange() => Fit();

        private void Fit()
        {
            if (_window == null) return;
            var size = ((RectTransform)transform).rect.size;
            _window.localScale = Vector3.one * Mathf.Min(size.x / 1440f, size.y / 1080f);
        }
    }
}
