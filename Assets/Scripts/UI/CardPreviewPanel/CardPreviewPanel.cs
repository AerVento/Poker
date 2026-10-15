using System.Collections.Generic;
using Framework.UI;
using Game.Content;
using Game.Input;
using Game.Network;
using Game.SO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;

namespace Game.UI
{
    public sealed class CardPreviewPanel : SingletonPanel<CardPreviewPanel>, IPointerDownHandler,
        IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        [SerializeField] private CardFaceView _view;
        [SerializeField, Min(1f)] private float _maximumScale = 4f;
        [SerializeField, Min(0f)] private float _scrollSensitivity = 0.15f;
        [SerializeField, Min(0f)] private float _doubleClickSeconds = 0.3f;

        private sealed class PointerState
        {
            public int Id;
            public Vector2 ScreenStart;
            public Vector2 Position;
            public bool StartedOnCard;
            public bool CancelClick;
        }

        private readonly List<PointerState> _pointers = new List<PointerState>();
        private readonly CardPreviewTransform _pose = new CardPreviewTransform();
        private readonly RemoteAssetBinding<PokerCardsSO> _binding = new RemoteAssetBinding<PokerCardsSO>();
        private PokerElement _source;
        private GamePanel _owner;
        private PanelShortcutHandler _shortcuts;
        private Poker _poker;
        private bool _showFront;
        private string _handId;
        private int? _roomId;
        private GameStage _stage;
        private Camera _eventCamera;
        private Vector2 _previousCentre;
        private float _previousDistance;
        private Vector2 _layoutSize;
        private Vector2 _lastClickPosition;
        private float _lastClickTime = float.NegativeInfinity;
        private bool _lastClickWasTouch;

        // The full-screen graphic owns pointer hits. The game keeps its keyboard focus and controls.
        public override bool ParticipatesInNavigation => false;

        public static void Open(PokerElement source)
        {
            var owner = source == null ? null : source.GetComponentInParent<GamePanel>();
            if (owner == null || !source.isActiveAndEnabled || !UIManager.Available ||
                UIManager.Instance.TopPanelIdentifier != owner.Identifier)
                return;
            var panel = UIManager.Instance.PushPanel<CardPreviewPanel>(UIManager.Layer.Top);
            if (panel != null)
                panel.Present(source, owner);
        }

        public override void Show()
        {
            base.Show();
            transform.SetAsLastSibling();
        }

        private void Present(PokerElement source, GamePanel owner)
        {
            ReleaseContent();
            _source = source;
            _owner = owner;
            _poker = source.Poker;
            _showFront = source.IsShowing;
            _handId = owner.Info.HandId;
            _roomId = owner.Info.RoomInfo?.Id;
            _stage = owner.Info.Round;
            _shortcuts = owner.GetComponent<PanelShortcutHandler>();
            if (_shortcuts != null)
                _shortcuts.ShortcutIntercepted += DismissShortcut;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            _eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            _pose.Reset();
            _lastClickTime = float.NegativeInfinity;
            var face = CardFaceAppearance.Current;
            _binding.Bind(face == null ? null : face.Cards, cards =>
            {
                _view.SetCards(cards != null ? cards : CardFaceAppearance.ClassicCards);
                _view.Display(_poker, _showFront);
                Layout();
            });
            Layout();
            // Make the new overlay raycastable even if another touch is processed this frame.
            Canvas.ForceUpdateCanvases();
        }

        private bool DismissShortcut(PokerShortcutAction action)
        {
            if (action != PokerShortcutAction.Back || !IsShowing)
                return false;
            Close();
            return true;
        }

        public void CloseFor(PokerElement source)
        {
            if (IsShowing && _source == source)
                Close();
        }

        private void Close()
        {
            if (UIManager.Available)
                UIManager.Instance.PopPanel<CardPreviewPanel>();
            else
                Hide();
        }

        private void OnDisable() => ReleaseContent();

        private void ReleaseContent()
        {
            if (_shortcuts != null)
                _shortcuts.ShortcutIntercepted -= DismissShortcut;
            _shortcuts = null;
            if (_view != null)
                _view.SetCards(null);
            _binding.Dispose();
            _source = null;
            _owner = null;
            ClearGesture();
        }

        private void LateUpdate()
        {
            if (_owner == null || _source == null || !_source.isActiveAndEnabled ||
                UIManager.Instance.TopPanelIdentifier != _owner.Identifier ||
                _owner.Info.RoomInfo?.Id != _roomId || _owner.Info.HandId != _handId ||
                (_stage < GameStage.RoundOver && _owner.Info.Round >= GameStage.RoundOver) ||
                _source.IsShowing != _showFront || (_showFront && !_source.Poker.Is(_poker)))
            {
                Close();
                return;
            }
            if (((RectTransform)transform).rect.size != _layoutSize)
                Layout();
            UpdateGesture();
        }

        private void Layout()
        {
            _layoutSize = ((RectTransform)transform).rect.size;
            var sprite = _view.Image.sprite;
            float aspect = sprite == null ? 17f / 24f : sprite.rect.width / sprite.rect.height;
            var size = new Vector2(_layoutSize.y * aspect, _layoutSize.y);
            ((RectTransform)_view.transform).sizeDelta = size;
            ApplyPose();
            RebaseGesture();
        }

        private void ApplyPose()
        {
            var rect = (RectTransform)_view.transform;
            rect.localScale = Vector3.one * _pose.Scale;
            rect.anchoredPosition = _pose.Position;
        }

        private bool OnCard(Vector2 screenPosition) =>
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_view.transform,
                screenPosition, _eventCamera);

        private Vector2 LocalPoint(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screenPosition,
                _eventCamera, out var point);
            return point;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _source == null ||
                _pointers.Exists(pointer => pointer.Id == eventData.pointerId))
                return;
            _pointers.Add(new PointerState
            {
                Id = eventData.pointerId,
                ScreenStart = eventData.position,
                Position = LocalPoint(eventData.position),
                StartedOnCard = OnCard(eventData.position),
            });
            if (_pointers.Count > 1)
            {
                foreach (var pointer in _pointers)
                    pointer.CancelClick = true;
                _lastClickTime = float.NegativeInfinity;
            }
            RebaseGesture();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var pointer = _pointers.Find(value => value.Id == eventData.pointerId);
            if (pointer != null)
                pointer.CancelClick = true;
            _lastClickTime = float.NegativeInfinity;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var pointer = _pointers.Find(value => value.Id == eventData.pointerId);
            if (pointer != null)
                pointer.Position = LocalPoint(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData) { }

        public void OnPointerUp(PointerEventData eventData)
        {
            var pointer = _pointers.Find(value => value.Id == eventData.pointerId);
            if (pointer == null)
                return;
            pointer.Position = LocalPoint(eventData.position);
            UpdateGesture();
            int threshold = EventSystem.current == null ? 10 : EventSystem.current.pixelDragThreshold;
            bool cancelled = IsCancelledTouch(eventData);
            bool click = !cancelled && !pointer.CancelClick && _pointers.Count == 1 &&
                (eventData.position - pointer.ScreenStart).sqrMagnitude <= threshold * threshold;
            _pointers.Remove(pointer);
            RebaseGesture();
            if (!click)
                return;
            bool onCard = OnCard(eventData.position);
            if (!pointer.StartedOnCard && !onCard)
            {
                Close();
                return;
            }
            if (!pointer.StartedOnCard || !onCard)
                return;
            bool isTouch = eventData is ExtendedPointerEventData data && data.pointerType == UIPointerType.Touch;
            float now = Time.unscaledTime;
            float doubleClickDistance = Mathf.Max(24f, threshold * 2f);
            if (isTouch == _lastClickWasTouch && now - _lastClickTime <= _doubleClickSeconds &&
                (eventData.position - _lastClickPosition).sqrMagnitude <= doubleClickDistance * doubleClickDistance)
            {
                _pose.Reset();
                ApplyPose();
                _lastClickTime = float.NegativeInfinity;
            }
            else
            {
                _lastClickTime = now;
                _lastClickPosition = eventData.position;
                _lastClickWasTouch = isTouch;
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_source == null || _pointers.Count > 0 || !OnCard(eventData.position))
                return;
            var point = LocalPoint(eventData.position);
            float scroll = Mathf.Clamp(eventData.scrollDelta.y, -1f, 1f);
            _pose.Zoom(Mathf.Exp(scroll * _scrollSensitivity), point, point, _maximumScale);
            ApplyPose();
            _lastClickTime = float.NegativeInfinity;
        }

        private void RebaseGesture()
        {
            _previousCentre = _pointers.Count == 0 ? Vector2.zero : _pointers[0].Position;
            _previousDistance = 0;
            if (_pointers.Count >= 2)
            {
                _previousCentre = (_pointers[0].Position + _pointers[1].Position) * 0.5f;
                _previousDistance = Vector2.Distance(_pointers[0].Position, _pointers[1].Position);
            }
        }

        private static bool IsCancelledTouch(PointerEventData eventData)
        {
            if (!(eventData is ExtendedPointerEventData extended))
                return false;
            // InputSystemUIInputModule can supply a touch's position/press child control.
            var control = extended.control;
            while (control != null && !(control is TouchControl))
                control = control.parent;
            return control is TouchControl touch &&
                touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled;
        }

        private void UpdateGesture()
        {
            if (_pointers.Count == 0)
                return;
            if (_pointers.Count == 1)
            {
                if (_pointers[0].StartedOnCard)
                    _pose.Pan(_pointers[0].Position - _previousCentre);
            }
            else if (_pointers[0].StartedOnCard || _pointers[1].StartedOnCard)
            {
                var centre = (_pointers[0].Position + _pointers[1].Position) * 0.5f;
                float distance = Vector2.Distance(_pointers[0].Position, _pointers[1].Position);
                if (_previousDistance > 0.01f && distance > 0.01f)
                    _pose.Zoom(distance / _previousDistance, _previousCentre, centre, _maximumScale);
            }
            RebaseGesture();
            ApplyPose();
        }

        private void ClearGesture()
        {
            _pointers.Clear();
            _lastClickTime = float.NegativeInfinity;
            RebaseGesture();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                ClearGesture();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                ClearGesture();
        }
    }
}
