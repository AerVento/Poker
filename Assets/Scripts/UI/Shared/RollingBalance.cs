using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // Visual-only: new authoritative values can interrupt an unfinished transition.
    public sealed class RollingBalance : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _deltaLabel;
        [SerializeField] private GameObject _statusDuringPlay;
        private bool _restoreStatus;
        [SerializeField, Min(0f)] private float _duration = 0.5f;
        [SerializeField, Min(0f), Tooltip("Seconds the profit/loss text stays fully visible.")]
        private float _deltaHoldDuration = 1f;
        [SerializeField, Min(0f), Tooltip("Seconds the profit/loss text takes to fade out after the hold.")]
        private float _deltaFadeDuration = 2f;
        [SerializeField] private Color _profitColor = new Color(0.42f, 0.84f, 0.59f);
        [SerializeField] private Color _lossColor = new Color(0.95f, 0.40f, 0.40f);
        private readonly List<Digit> _digits = new List<Digit>();
        private uint _value;
        private bool _initialized;
        private bool _rolling;
        private float _started;
        private float _deltaStarted;
        private bool _showingDelta;
        private int _direction;
        private Color _deltaColor;
        private int _lengthChange;
        private const float LayoutDurationRatio = 0.2f;

        private sealed class Digit
        {
            public RectTransform Clip;
            public TextMeshProUGUI Current;
            public TextMeshProUGUI Next;
            public char From;
            public char To;
            public int Steps;
            public float CompactX;
            public float ExpandedX;
        }

        public uint Value => _value;

        public void SetValue(uint value, bool animate = true)
        {
            if (_label == null) return;
            if (_initialized && value == _value) return;
            var previous = _value;
            bool play = animate && _initialized && isActiveAndEnabled && _duration > 0;
            _value = value;
            _initialized = true;
            FinishRoll();
            _label.text = "$" + ChipAmountFormatter.Format(value);
            if (!play) return;
            _direction = value > previous ? 1 : -1;
            PrepareDigits(previous, value);
            _started = Time.unscaledTime;
            _rolling = true;
            _label.enabled = false;
            UpdateBalanceAnimation(0f);
        }

        public void ResetValue(uint value)
        {
            _initialized = false;
            ClearDelta();
            SetValue(value, false);
        }

        public void ShowDelta(long net)
        {
            ClearDelta();
            if (_deltaLabel == null || net == 0 || (_deltaHoldDuration <= 0 && _deltaFadeDuration <= 0) || !isActiveAndEnabled) return;
            _deltaColor = net > 0 ? _profitColor : _lossColor;
            _deltaLabel.color = _deltaColor;
            _deltaLabel.text = "(" + (net > 0 ? "+$" : "-$") + ChipAmountFormatter.Format((uint)Math.Min(uint.MaxValue, Math.Abs(net))) + ")";
            _deltaLabel.gameObject.SetActive(true);
            _restoreStatus = _statusDuringPlay != null && _statusDuringPlay.activeSelf;
            if (_restoreStatus) _statusDuringPlay.SetActive(false);
            _deltaStarted = Time.unscaledTime;
            _showingDelta = true;
        }

        private void PrepareDigits(uint previous, uint next)
        {
            string from = ChipAmountFormatter.Format(previous);
            string to = ChipAmountFormatter.Format(next);
            int fromLength = from.Length;
            int toLength = to.Length;
            _lengthChange = toLength - fromLength;
            int count = Math.Max(fromLength, toLength);
            float[] fromPositions = MeasurePositions("$" + from);
            float[] toPositions = MeasurePositions("$" + to);
            from = "$" + from.PadLeft(count, ' ');
            to = "$" + to.PadLeft(count, ' ');
            _label.ForceMeshUpdate();
            float fontSize = _label.fontSize;
            float width = _label.GetPreferredValues("0").x;
            float height = _label.rectTransform.rect.height;
            float available = _label.rectTransform.rect.width;
            if (width * from.Length > available && available > 0)
            {
                float scale = available / (width * from.Length);
                fontSize *= scale;
                width *= scale;
            }
            width = Mathf.Max(1f, width);
            for (int i = 0; i < from.Length; i++)
            {
                if (_digits.Count <= i) _digits.Add(CreateDigit());
                var digit = _digits[i];
                digit.Clip.gameObject.SetActive(true);
                digit.Clip.anchorMin = digit.Clip.anchorMax = new Vector2(0f, 0.5f);
                digit.Clip.pivot = new Vector2(0f, 0.5f);
                int compactIndex = i == 0 ? 0 : Mathf.Max(1, i - Math.Abs(_lengthChange));
                var expandedPositions = _lengthChange >= 0 ? toPositions : fromPositions;
                var compactPositions = _lengthChange >= 0 ? fromPositions : toPositions;
                digit.ExpandedX = expandedPositions[i];
                digit.CompactX = compactPositions[compactIndex];
                digit.Clip.anchoredPosition = new Vector2(digit.ExpandedX, 0f);
                digit.Clip.sizeDelta = new Vector2(width + 1f, height);
                digit.Current.fontSize = digit.Next.fontSize = fontSize;
                digit.Current.color = digit.Next.color = _label.color;
                digit.From = from[i];
                digit.To = to[i];
                int a = char.IsDigit(from[i]) ? from[i] - '0' : 0;
                int b = char.IsDigit(to[i]) ? to[i] - '0' : 0;
                digit.Steps = from[i] == to[i] ? 0 :
                    (!char.IsDigit(from[i]) || !char.IsDigit(to[i])) ? 1 : 10 + (_direction > 0 ? (b - a + 10) % 10 : (a - b + 10) % 10);
            }
            for (int i = from.Length; i < _digits.Count; i++) _digits[i].Clip.gameObject.SetActive(false);
        }

        private float[] MeasurePositions(string text)
        {
            _label.text = text;
            _label.ForceMeshUpdate(true, true);
            var positions = new float[text.Length];
            for (int i = 0; i < positions.Length; i++)
                positions[i] = i < _label.textInfo.characterCount
                    ? _label.textInfo.characterInfo[i].origin - _label.rectTransform.rect.xMin
                    : _label.GetPreferredValues(text.Substring(0, i)).x;
            return positions;
        }

        private Digit CreateDigit()
        {
            var clip = new GameObject("RollingDigit", typeof(RectTransform), typeof(RectMask2D));
            clip.transform.SetParent(_label.transform, false);
            return new Digit { Clip = (RectTransform)clip.transform,
                Current = CreateGlyph(clip.transform), Next = CreateGlyph(clip.transform) };
        }

        private TextMeshProUGUI CreateGlyph(Transform parent)
        {
            var obj = new GameObject("Glyph", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var label = obj.AddComponent<TextMeshProUGUI>();
            label.font = _label.font;
            label.fontSharedMaterial = _label.fontSharedMaterial;
            label.fontStyle = _label.fontStyle;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return label;
        }

        private void Update()
        {
            if (_rolling)
            {
                float elapsed = Time.unscaledTime - _started;
                UpdateBalanceAnimation(elapsed);
                float total = _duration * (_lengthChange == 0 ? 1f : 1f + LayoutDurationRatio);
                if (elapsed >= total) FinishRoll();
            }
            if (_showingDelta)
            {
                float elapsed = Time.unscaledTime - _deltaStarted;
                float hold = Mathf.Max(0f, _deltaHoldDuration);
                float fade = Mathf.Max(0f, _deltaFadeDuration);
                if (elapsed >= hold + fade)
                {
                    ClearDelta();
                    return;
                }
                float progress = fade > 0f ? Mathf.Clamp01((elapsed - hold) / fade) : 0f;
                var color = _deltaColor;
                color.a *= 1f - Mathf.SmoothStep(0f, 1f, progress);
                _deltaLabel.color = color;
            }
        }

        private void UpdateBalanceAnimation(float elapsed)
        {
            float layoutDuration = Mathf.Max(0.001f, _duration * LayoutDurationRatio);
            float rollElapsed = _lengthChange > 0 ? elapsed - layoutDuration : elapsed;
            float progress = Mathf.Clamp01(rollElapsed / Mathf.Max(0.001f, _duration));
            UpdateRoll(1f - Mathf.Pow(1f - progress, 3f));
            if (_lengthChange == 0) return;

            // Make room before adding digits; close the gap after removed digits have left.
            float layoutProgress = Mathf.Clamp01((_lengthChange > 0 ? elapsed : elapsed - _duration) / layoutDuration);
            float expanded = _lengthChange > 0
                ? Mathf.SmoothStep(0f, 1f, layoutProgress)
                : 1f - Mathf.SmoothStep(0f, 1f, layoutProgress);
            foreach (var digit in _digits)
            {
                if (!digit.Clip.gameObject.activeSelf) continue;
                digit.Clip.anchoredPosition = new Vector2(Mathf.Lerp(digit.CompactX, digit.ExpandedX, expanded), 0f);
            }
        }

        private void UpdateRoll(float progress)
        {
            foreach (var digit in _digits)
            {
                if (!digit.Clip.gameObject.activeSelf) continue;
                if (digit.Steps == 0)
                {
                    digit.Current.text = digit.To.ToString();
                    digit.Current.rectTransform.anchoredPosition = Vector2.zero;
                    digit.Next.text = string.Empty;
                    continue;
                }
                float travel = progress * digit.Steps;
                int step = Mathf.FloorToInt(travel);
                float fraction = travel - step;
                int start = char.IsDigit(digit.From) ? digit.From - '0' : 0;
                int current = (start + _direction * step % 10 + 10) % 10;
                int following = (current + _direction + 10) % 10;
                digit.Current.text = step == 0 ? digit.From.ToString() :
                    step >= digit.Steps ? digit.To.ToString() : current.ToString();
                digit.Next.text = step + 1 >= digit.Steps ? digit.To.ToString() : following.ToString();
                float distance = digit.Clip.rect.height;
                digit.Current.rectTransform.anchoredPosition = new Vector2(0f, _direction * fraction * distance);
                digit.Next.rectTransform.anchoredPosition = new Vector2(0f, _direction * (fraction - 1f) * distance);
            }
        }

        private void FinishRoll()
        {
            _rolling = false;
            if (_label != null) _label.enabled = true;
            foreach (var digit in _digits) if (digit.Clip != null) digit.Clip.gameObject.SetActive(false);
        }

        private void ClearDelta()
        {
            _showingDelta = false;
            if (_restoreStatus && _statusDuringPlay != null) _statusDuringPlay.SetActive(true);
            _restoreStatus = false;
            if (_deltaLabel != null) _deltaLabel.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            FinishRoll();
            ClearDelta();
            _initialized = false;
        }
    }
}
