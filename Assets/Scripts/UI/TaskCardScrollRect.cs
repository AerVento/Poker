using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class TaskCardScrollRect : ScrollRect
    {
        public int CurrentIndex { get; private set; }
        public int PageCount { get; private set; }
        public event Action<int, int> PageChanged;
        private bool _dragging;
        private float _stride;
        private float _dragStart;

        public void SetPages(int count, float stride, int index)
        {
            PageCount = Mathf.Max(0, count);
            _stride = stride;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            SelectPage(index, true);
        }

        public void SelectPage(int index, bool immediate = false)
        {
            CurrentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, PageCount - 1));
            StopMovement();
            if (immediate && content != null)
                content.anchoredPosition = new Vector2(-CurrentIndex * _stride, 0);
            PageChanged?.Invoke(CurrentIndex, PageCount);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            base.OnBeginDrag(eventData);
            _dragging = true;
            _dragStart = content.anchoredPosition.x;
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            base.OnEndDrag(eventData);
            if (!_dragging || eventData.button != PointerEventData.InputButton.Left) return;
            _dragging = false;
            var delta = content.anchoredPosition.x - _dragStart;
            var nearest = _stride > 0 ? Mathf.RoundToInt(-content.anchoredPosition.x / _stride) : 0;
            // A deliberate short swipe also advances one card; a tap settles back in place.
            if (nearest == CurrentIndex && Mathf.Abs(delta) > 80)
                nearest += delta < 0 ? 1 : -1;
            SelectPage(nearest);
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (_dragging) return;
            var delta = Mathf.Abs(eventData.scrollDelta.x) > Mathf.Abs(eventData.scrollDelta.y)
                ? -eventData.scrollDelta.x : eventData.scrollDelta.y;
            if (Mathf.Abs(delta) > 0.01f) SelectPage(CurrentIndex + (delta < 0 ? 1 : -1));
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (_dragging || content == null) return;
            var target = -CurrentIndex * _stride;
            var x = Mathf.Lerp(content.anchoredPosition.x, target, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
            if (Mathf.Abs(x - target) < 0.1f) x = target;
            content.anchoredPosition = new Vector2(x, 0);
        }

        protected override void OnDisable()
        {
            _dragging = false;
            base.OnDisable();
        }
    }
}
