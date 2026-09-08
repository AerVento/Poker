using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class TaskCardScrollTest
    {
        private GameObject _root;
        private TaskCardScrollRect _scroll;
        private EventSystem _events;

        [SetUp]
        public void Setup()
        {
            _root = new GameObject("Task scroll test", typeof(RectTransform), typeof(Canvas));
            var view = new GameObject("View", typeof(RectTransform), typeof(TaskCardScrollRect));
            view.transform.SetParent(_root.transform, false);
            _scroll = view.GetComponent<TaskCardScrollRect>();
            _scroll.viewport = (RectTransform)view.transform;
            _scroll.viewport.sizeDelta = new Vector2(640, 560);
            _scroll.horizontal = true;
            _scroll.vertical = false;
            _scroll.inertia = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.content = (RectTransform)new GameObject("Cards", typeof(RectTransform)).transform;
            _scroll.content.SetParent(view.transform, false);
            _scroll.content.anchorMin = _scroll.content.anchorMax = new Vector2(0, 0.5f);
            _scroll.content.pivot = new Vector2(0, 0.5f);
            _scroll.content.sizeDelta = new Vector2(2048, 560);
            for (var i = 0; i < 3; i++)
            {
                var card = (RectTransform)new GameObject("Card", typeof(RectTransform)).transform;
                card.SetParent(_scroll.content, false);
                card.anchorMin = card.anchorMax = new Vector2(0, 0.5f);
                card.sizeDelta = new Vector2(640, 514);
                card.anchoredPosition = new Vector2(320 + i * 704, 0);
            }
            _events = new GameObject("Task event test", typeof(EventSystem)).GetComponent<EventSystem>();
            _scroll.SetPages(3, 704, 0);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_events.gameObject);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EveryPageCentersExactlyOneCardAndClipsNeighbors(int page)
        {
            _scroll.SelectPage(page, true);
            var view = _scroll.viewport;
            var card = (RectTransform)_scroll.content.GetChild(page);
            var center = view.InverseTransformPoint(card.TransformPoint(card.rect.center));
            Assert.AreEqual(view.rect.center.x, center.x, 0.01f);
            Assert.AreEqual(view.rect.width, card.rect.width);
            for (var i = 0; i < 3; i++)
            {
                if (i == page) continue;
                var other = (RectTransform)_scroll.content.GetChild(i);
                var x = view.InverseTransformPoint(other.TransformPoint(other.rect.center)).x;
                Assert.Greater(Mathf.Abs(x - view.rect.center.x), (view.rect.width + other.rect.width) / 2);
            }
        }

        [TestCase(-30, 1)]
        [TestCase(-120, 2)]
        [TestCase(120, 0)]
        public void ReleasingDragSettlesToCurrentOrAdjacentCard(float delta, int expected)
        {
            _scroll.SelectPage(1, true);
            var data = new PointerEventData(_events) { button = PointerEventData.InputButton.Left };
            _scroll.OnBeginDrag(data);
            _scroll.content.anchoredPosition += new Vector2(delta, 0);
            _scroll.OnEndDrag(data);
            Assert.AreEqual(expected, _scroll.CurrentIndex);
            Assert.AreEqual(Vector2.zero, _scroll.velocity);
        }

        [Test]
        public void DeletingLastCardClampsSelectionAndWheelCannotPassBounds()
        {
            _scroll.SelectPage(2, true);
            _scroll.SetPages(2, 704, _scroll.CurrentIndex);
            Assert.AreEqual(1, _scroll.CurrentIndex);
            Assert.AreEqual(-704, _scroll.content.anchoredPosition.x);
            _scroll.OnScroll(new PointerEventData(_events) { scrollDelta = new Vector2(0, -1) });
            Assert.AreEqual(1, _scroll.CurrentIndex);
            _scroll.SetPages(0, 704, 1);
            Assert.AreEqual(0, _scroll.CurrentIndex);
            Assert.AreEqual(0, _scroll.content.anchoredPosition.x);
        }
    }
}
