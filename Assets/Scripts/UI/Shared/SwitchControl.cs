using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [RequireComponent(typeof(Toggle))]
    public sealed class SwitchControl : MonoBehaviour
    {
        [SerializeField] private Toggle _toggle;
        [SerializeField] private Image _track;
        [SerializeField] private Image _handle;
        [SerializeField] private Color _offTrackColor = new(0.133f, 0.239f, 0.224f, 1f);
        [SerializeField] private Color _onTrackColor = new(0.851f, 0.737f, 0.478f, 1f);
        [SerializeField] private Color _offHandleColor = new(0.643f, 0.725f, 0.678f, 1f);
        [SerializeField] private Color _onHandleColor = new(0.043f, 0.09f, 0.098f, 1f);
        [SerializeField] private float _handleOffset = 18f;
        [SerializeField, Range(0.4f, 1f)] private float _trackWidthRatio = 0.62f;
        [SerializeField, Range(0.4f, 1f)] private float _trackHeightRatio = 0.68f;
        [SerializeField, Range(0.5f, 0.95f)] private float _handleSizeRatio = 0.8f;

        public event Action<bool> ValueChanged;

        public bool IsOn
        {
            get => ResolveToggle().isOn;
            set => ResolveToggle().isOn = value;
        }

        private void Awake()
        {
            ResolveToggle();
            RefreshGeometry();
            RefreshVisual(_toggle.isOn);
        }

        private void OnEnable()
        {
            ResolveToggle().onValueChanged.AddListener(HandleValueChanged);
            RefreshGeometry();
            RefreshVisual(_toggle.isOn);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled)
                return;
            RefreshGeometry();
            RefreshVisual(ResolveToggle().isOn);
        }

        private void OnDisable()
        {
            if (_toggle != null)
                _toggle.onValueChanged.RemoveListener(HandleValueChanged);
        }

        public void SetIsOnWithoutNotify(bool value)
        {
            ResolveToggle().SetIsOnWithoutNotify(value);
            RefreshVisual(value);
        }

        private Toggle ResolveToggle()
        {
            if (_toggle == null)
                _toggle = GetComponent<Toggle>();
            return _toggle;
        }

        private void HandleValueChanged(bool value)
        {
            RefreshVisual(value);
            ValueChanged?.Invoke(value);
        }

        private void RefreshVisual(bool value)
        {
            if (_track != null)
                _track.color = value ? _onTrackColor : _offTrackColor;
            if (_handle != null)
            {
                _handle.color = value ? _onHandleColor : _offHandleColor;
                var handleTransform = _handle.rectTransform;
                var position = handleTransform.anchoredPosition;
                float offset = CalculateHandleOffset();
                position.x = value ? offset : -offset;
                handleTransform.anchoredPosition = position;
            }
        }

        private void RefreshGeometry()
        {
            if (_track == null || _handle == null)
                return;

            var rootRect = transform as RectTransform;
            if (rootRect == null || rootRect.rect.width <= 0f || rootRect.rect.height <= 0f)
                return;

            float trackHeight = rootRect.rect.height * _trackHeightRatio;
            float trackWidth = Mathf.Min(rootRect.rect.width * _trackWidthRatio, trackHeight * 1.82f);
            var trackRect = _track.rectTransform;
            trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.anchoredPosition = Vector2.zero;
            trackRect.sizeDelta = new Vector2(trackWidth, trackHeight);

            float handleSize = trackHeight * _handleSizeRatio;
            var handleRect = _handle.rectTransform;
            handleRect.anchorMin = handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(handleSize, handleSize);
        }

        private float CalculateHandleOffset()
        {
            if (_track == null || _handle == null)
                return _handleOffset;
            return Mathf.Max(0f, (_track.rectTransform.rect.width - _handle.rectTransform.rect.width) * 0.5f -
                                 _track.rectTransform.rect.height * 0.08f);
        }
    }
}
