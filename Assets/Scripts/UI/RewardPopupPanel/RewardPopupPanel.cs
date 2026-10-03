using System;
using System.Collections.Generic;
using Framework.UI;
using Game.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class RewardPopupPanel : SingletonPanel<RewardPopupPanel>
    {
        [SerializeField] private RectTransform _window;
        [SerializeField] private RectTransform _items;
        [SerializeField] private RewardItemView _itemPrefab;
        [SerializeField] private Button _close;
        [SerializeField] private TextMeshProUGUI _title;
        private readonly List<RewardItemView> _views = new List<RewardItemView>();
        private Action _closed;
        private void OnEnable() { _close.onClick.AddListener(Close); Fit(); }
        private void OnDisable()
        {
            _close.onClick.RemoveListener(Close);
            _closed = null;
            foreach (var view in _views) if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            _views.Clear();
        }
        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (_window == null) return;
            var size = ((RectTransform)transform).rect.size;
            _window.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
        }
        public void Present(IEnumerable<RewardEntry> items, Action closed = null, string title = "获得奖励")
        {
            _closed = closed;
            _title.text = title;
            foreach (var view in _views) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            _views.Clear();
            foreach (var item in items)
            {
                var view = Instantiate(_itemPrefab, _items);
                view.gameObject.SetActive(true);
                view.Bind(item);
                _views.Add(view);
            }
        }
        private void Close()
        {
            var closed = _closed;
            UIManager.Instance.PopPanel<RewardPopupPanel>();
            closed?.Invoke();
        }
    }
}
