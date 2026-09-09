using System;
using System.Collections.Generic;
using Framework.UI;
using Game.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class AchievementBannerPanel : SingletonPanel<AchievementBannerPanel>
    {
        [SerializeField] private RectTransform _banner;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private float _duration = 4.5f;
        private readonly Queue<(AchievementView View, Action Shown)> _queue = new Queue<(AchievementView, Action)>();
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private Action _acknowledge;
        private float _elapsed;
        private bool _displaying;
        public override bool ParticipatesInNavigation => false;

        public void Enqueue(AchievementView view, Action shown)
        {
            if (string.IsNullOrEmpty(view.Id) || !_seen.Add(view.Id)) return;
            _queue.Enqueue((view, shown));
        }

        private void OnEnable()
        {
            _description.enableWordWrapping = true;
            _description.overflowMode = TextOverflowModes.Ellipsis;
            _description.richText = false;
            _group.alpha = 0;
            _group.interactable = _group.blocksRaycasts = false;
        }

        private void Update()
        {
            if (!_displaying && _queue.Count > 0)
            {
                var item = _queue.Dequeue();
                _icon.sprite = AchievementRow.LoadIcon(item.View.IconPath);
                _name.text = item.View.Name;
                _description.text = item.View.Description;
                _acknowledge = item.Shown;
                _elapsed = 0;
                _displaying = true;
            }
            if (!_displaying) return;
            _elapsed += Time.unscaledDeltaTime;
            var enter = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_elapsed / 0.3f));
            var leave = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_duration - _elapsed) / 0.3f));
            _group.alpha = Mathf.Min(enter, leave);
            var size = ((RectTransform)transform).rect.size;
            var scale = Mathf.Min(1, Mathf.Min(size.x / 1000f, size.y / 700f));
            _banner.localScale = Vector3.one * scale;
            _banner.anchoredPosition = new Vector2(0, -24 - (1 - enter) * -_banner.rect.height);
            if (_elapsed < _duration) return;
            _displaying = false;
            _group.alpha = 0;
            var acknowledge = _acknowledge;
            _acknowledge = null;
            acknowledge?.Invoke();
        }

        private void OnDisable()
        {
            _displaying = false;
            _acknowledge = null;
            _queue.Clear();
            _seen.Clear();
        }
    }
}
