using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class SlotRewardScrollRect : ScrollRect
    {
        private ScrollRect _parentScroll;
        private bool _forwardDrag;

        private ScrollRect ParentScroll
        {
            get
            {
                if (_parentScroll == null && transform.parent != null)
                    _parentScroll = transform.parent.GetComponentInParent<ScrollRect>();
                return _parentScroll;
            }
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (ParentScroll != null && Mathf.Abs(eventData.scrollDelta.y) >= Mathf.Abs(eventData.scrollDelta.x))
                ParentScroll.OnScroll(eventData);
            else base.OnScroll(eventData);
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            base.OnInitializePotentialDrag(eventData);
            if (ParentScroll != null) ParentScroll.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            _forwardDrag = ParentScroll != null && Mathf.Abs(eventData.delta.y) > Mathf.Abs(eventData.delta.x);
            if (_forwardDrag) ParentScroll.OnBeginDrag(eventData);
            else base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (_forwardDrag && ParentScroll != null) ParentScroll.OnDrag(eventData);
            else base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (_forwardDrag && ParentScroll != null) ParentScroll.OnEndDrag(eventData);
            else base.OnEndDrag(eventData);
            _forwardDrag = false;
        }
    }
}
