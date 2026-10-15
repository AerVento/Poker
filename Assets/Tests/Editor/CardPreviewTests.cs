using System.Reflection;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.Tests
{
    public sealed class CardPreviewTests
    {
        [Test]
        public void ZoomAnchorsTheInspectedPointAndFollowsThePinchCentre()
        {
            var pose = new CardPreviewTransform();
            var point = new Vector2(40, 70);
            pose.Zoom(2f, point, point, 4f);
            Assert.That((point - pose.Position) / pose.Scale, Is.EqualTo(point));
            pose.Reset();
            pose.Zoom(2f, Vector2.zero, new Vector2(30, 20), 4f);
            Assert.That(pose.Position, Is.EqualTo(new Vector2(30, 20)));
        }

        [Test]
        public void PanIsFreeAtEveryScaleAndOnlyResetRecentres()
        {
            var pose = new CardPreviewTransform();
            pose.Pan(new Vector2(10000, -10000));
            Assert.That(pose.Position, Is.EqualTo(new Vector2(10000, -10000)));
            pose.Reset();
            pose.Zoom(100f, Vector2.zero, Vector2.zero, 4f);
            Assert.That(pose.Scale, Is.EqualTo(4f));
            pose.Pan(new Vector2(10000, -10000));
            Assert.That(pose.Position, Is.EqualTo(new Vector2(10000, -10000)));
            pose.Zoom(float.NaN, Vector2.zero, Vector2.zero, 4f);
            pose.Zoom(float.PositiveInfinity, Vector2.zero, Vector2.zero, 4f);
            Assert.That(pose.Scale, Is.EqualTo(4f));
            Assert.That(pose.Position, Is.EqualTo(new Vector2(10000, -10000)));
            pose.Zoom(0.001f, Vector2.zero, Vector2.zero, 4f);
            Assert.That(pose.Scale, Is.EqualTo(0.5f));
            Assert.That(pose.Position, Is.EqualTo(new Vector2(1250, -1250)));
            pose.Pan(new Vector2(10000, -10000));
            Assert.That(pose.Position, Is.EqualTo(new Vector2(11250, -11250)));
            pose.Reset();
            Assert.That(pose.Scale, Is.EqualTo(1f));
            Assert.That(pose.Position, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void SevenCardsHaveHitAreasAndPreviewKeepsKeyboardOwnership()
        {
            var game = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/GamePanel/GamePanel.prefab");
            var cards = game.GetComponentsInChildren<PokerElement>(true);
            Assert.That(cards.Length, Is.EqualTo(7));
            foreach (var card in cards)
            {
                var triggers = card.GetComponentsInChildren<CardPreviewTrigger>(true);
                Assert.That(triggers.Length, Is.EqualTo(1));
                Assert.That(new SerializedObject(triggers[0]).FindProperty("_source").objectReferenceValue, Is.EqualTo(card));
                var image = triggers[0].GetComponent<UnityEngine.UI.Image>();
                Assert.That(image.enabled && image.raycastTarget, Is.True);
                Assert.That(image.color.a, Is.Zero);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/CardPreviewPanel/CardPreviewPanel.prefab");
            Assert.That(prefab.GetComponent<CardPreviewPanel>().ParticipatesInNavigation, Is.False);
            Assert.That(prefab.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            Assert.That(prefab.GetComponent<UnityEngine.UI.Image>().color.a, Is.InRange(0.01f, 0.99f));
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.UI.RectMask2D>(true), Is.Empty);
            Assert.That(prefab.GetComponentInChildren<CardFaceView>(true).transform.parent, Is.EqualTo(prefab.transform));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DragAtOneTimesMovesTheWholeCardAndItsHitArea(bool touch)
        {
            WithPreview((panel, pose, events) =>
            {
                var pointer = Pointer(events, 1, Vector2.zero, touch);
                panel.OnPointerDown(pointer);
                panel.OnBeginDrag(pointer);
                pointer.position = new Vector2(600, 100);
                panel.OnDrag(pointer);
                Invoke(panel, "UpdateGesture");
                panel.OnPointerUp(pointer);
                Assert.That(pose.Scale, Is.EqualTo(1f));
                Assert.That(pose.Position, Is.EqualTo(pointer.position));
                panel.OnScroll(new PointerEventData(events) { position = Vector2.zero, scrollDelta = Vector2.up });
                Assert.That(pose.Scale, Is.EqualTo(1f));
                panel.OnScroll(new PointerEventData(events) { position = pointer.position, scrollDelta = Vector2.up });
                Assert.That(pose.Scale, Is.GreaterThan(1f));
                Assert.That(pose.Position, Is.EqualTo(pointer.position));
                for (int i = 0; i < 2; i++)
                {
                    var click = Pointer(events, 2, pointer.position, touch);
                    panel.OnPointerDown(click);
                    panel.OnPointerUp(click);
                }
                Assert.That(pose.Scale, Is.EqualTo(1f));
                Assert.That(pose.Position, Is.EqualTo(Vector2.zero));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MouseAndTouchDoubleClickResetScaleAndPosition(bool touch)
        {
            WithPreview((panel, pose, events) =>
            {
                pose.Zoom(2f, Vector2.zero, Vector2.zero, 4f);
                pose.Pan(new Vector2(40, 70));
                for (int i = 0; i < 2; i++)
                {
                    var pointer = Pointer(events, 1, Vector2.zero, touch);
                    panel.OnPointerDown(pointer);
                    panel.OnPointerUp(pointer);
                }
                Assert.That(pose.Scale, Is.EqualTo(1f));
                Assert.That(pose.Position, Is.EqualTo(Vector2.zero));
            });
        }

        [Test]
        public void PinchToSingleFingerDoesNotJumpOrBecomeADoubleTap()
        {
            WithPreview((panel, pose, events) =>
            {
                var first = Pointer(events, 1, new Vector2(-40, 0), true);
                var second = Pointer(events, 2, new Vector2(40, 0), true);
                panel.OnPointerDown(first);
                panel.OnPointerDown(second);
                panel.OnBeginDrag(first);
                first.position = new Vector2(-80, 0);
                second.position = new Vector2(80, 0);
                panel.OnDrag(first);
                panel.OnDrag(second);
                Invoke(panel, "UpdateGesture");
                Assert.That(pose.Scale, Is.EqualTo(2f).Within(0.001f));
                panel.OnPointerUp(second);
                Assert.That(pose.Position, Is.EqualTo(Vector2.zero));
                first.position += new Vector2(20, 0);
                panel.OnDrag(first);
                Invoke(panel, "UpdateGesture");
                Assert.That(pose.Position.x, Is.EqualTo(20f).Within(0.001f));
                panel.OnPointerUp(first);
                var click = Pointer(events, 3, Vector2.zero, true);
                panel.OnPointerDown(click);
                panel.OnPointerUp(click);
                Assert.That(pose.Scale, Is.EqualTo(2f).Within(0.001f));
            });
        }

        [Test]
        public void LargeWheelDeltaStillZoomsInSmallSteps()
        {
            WithPreview((panel, pose, events) =>
            {
                panel.OnScroll(new PointerEventData(events) { position = Vector2.zero, scrollDelta = new Vector2(0, 120) });
                Assert.That(pose.Scale, Is.EqualTo(Mathf.Exp(0.15f)).Within(0.001f));
                panel.OnScroll(new PointerEventData(events) { position = Vector2.zero, scrollDelta = new Vector2(0, -120) });
                Assert.That(pose.Scale, Is.EqualTo(1f).Within(0.001f));
            });
        }

        [Test]
        public void CancelledTouchChildControlCannotBecomeTheFirstOfADoubleTap()
        {
            var touchscreen = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Touchscreen>();
            try
            {
                WithPreview((panel, pose, events) =>
                {
                    pose.Zoom(2f, Vector2.zero, Vector2.zero, 4f);
                    var cancelled = (ExtendedPointerEventData)Pointer(events, 1, Vector2.zero, true);
                    cancelled.control = touchscreen.touches[0].position;
                    panel.OnPointerDown(cancelled);
                    UnityEngine.InputSystem.LowLevel.InputState.Change(touchscreen.touches[0].phase,
                        UnityEngine.InputSystem.TouchPhase.Canceled);
                    panel.OnPointerUp(cancelled);
                    var click = Pointer(events, 2, Vector2.zero, true);
                    panel.OnPointerDown(click);
                    panel.OnPointerUp(click);
                    Assert.That(pose.Scale, Is.EqualTo(2f));
                });
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(touchscreen);
            }
        }

        private static PointerEventData Pointer(EventSystem events, int id, Vector2 position, bool touch) =>
            new ExtendedPointerEventData(events)
            {
                pointerId = id,
                position = position,
                button = PointerEventData.InputButton.Left,
                pointerType = touch ? UIPointerType.Touch : UIPointerType.MouseOrPen,
            };

        private static void WithPreview(System.Action<CardPreviewPanel, CardPreviewTransform, EventSystem> test)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Panels/CardPreviewPanel/CardPreviewPanel.prefab"));
            var eventObject = new GameObject("PreviewTestEvents", typeof(EventSystem));
            var sourceObject = new GameObject("PreviewTestSource", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.sizeDelta = new Vector2(1000, 600);
                var panel = root.GetComponent<CardPreviewPanel>();
                var source = sourceObject.AddComponent<PokerElement>();
                typeof(CardPreviewPanel).GetField("_source", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, source);
                Invoke(panel, "Layout");
                var pose = (CardPreviewTransform)typeof(CardPreviewPanel).GetField("_pose",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                test(panel, pose, eventObject.GetComponent<EventSystem>());
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(eventObject);
            }
        }

        private static void Invoke(CardPreviewPanel panel, string method) =>
            typeof(CardPreviewPanel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
    }
}
