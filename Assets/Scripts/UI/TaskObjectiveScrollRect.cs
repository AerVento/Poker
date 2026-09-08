using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    // Horizontal gestures still browse cards when they start over the objective list.
    public sealed class TaskObjectiveScrollRect : ScrollRect
    {
        private ScrollRect _outer;
        private bool _route;
        private ScrollRect Outer => _outer != null ? _outer : _outer = GetComponentsInParent<ScrollRect>().FirstOrDefault(s => s != this);
        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            base.OnInitializePotentialDrag(eventData);
            Outer?.OnInitializePotentialDrag(eventData);
        }
        public override void OnBeginDrag(PointerEventData eventData)
        {
            _route = Outer != null && (content.rect.height <= viewport.rect.height || Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y));
            if (_route) Outer.OnBeginDrag(eventData);
            else base.OnBeginDrag(eventData);
        }
        public override void OnDrag(PointerEventData eventData)
        {
            if (_route) Outer.OnDrag(eventData);
            else base.OnDrag(eventData);
        }
        public override void OnEndDrag(PointerEventData eventData)
        {
            if (_route) Outer.OnEndDrag(eventData);
            else base.OnEndDrag(eventData);
            _route = false;
        }
        public override void OnScroll(PointerEventData eventData)
        {
            if (Outer != null && (content.rect.height <= viewport.rect.height || Mathf.Abs(eventData.scrollDelta.x) > Mathf.Abs(eventData.scrollDelta.y)))
                Outer.OnScroll(eventData);
            else base.OnScroll(eventData);
        }
    }
}
