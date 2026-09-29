using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class QuantityStepper : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private Button _minusTen;
        [SerializeField] private Button _minusOne;
        [SerializeField] private Button _plusOne;
        [SerializeField] private Button _plusTen;
        [SerializeField, Min(1)] private int _smallStep = 1;
        [SerializeField, Min(1)] private int _largeStep = 10;
        [SerializeField, Min(0)] private int _minimum = 1;
        private uint _maximum = uint.MaxValue;
        private bool _interactable = true;

        public void Configure(uint maximum)
        {
            _maximum = maximum;
            RefreshButtons();
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            RefreshButtons();
        }

        private void OnEnable()
        {
            _minusTen.onClick.AddListener(MinusTen);
            _minusOne.onClick.AddListener(MinusOne);
            _plusOne.onClick.AddListener(PlusOne);
            _plusTen.onClick.AddListener(PlusTen);
            _input.onValueChanged.AddListener(OnValueChanged);
            RefreshButtons();
        }

        private void OnDisable()
        {
            _minusTen.onClick.RemoveListener(MinusTen);
            _minusOne.onClick.RemoveListener(MinusOne);
            _plusOne.onClick.RemoveListener(PlusOne);
            _plusTen.onClick.RemoveListener(PlusTen);
            _input.onValueChanged.RemoveListener(OnValueChanged);
        }

        private void OnValueChanged(string _) => RefreshButtons();
        private void MinusTen() => Adjust(-_largeStep);
        private void MinusOne() => Adjust(-_smallStep);
        private void PlusOne() => Adjust(_smallStep);
        private void PlusTen() => Adjust(_largeStep);
        private long Minimum => Math.Min((long)_maximum, Math.Max(0, _minimum));
        private long Value => long.TryParse(_input.text, out var value) ? Math.Max(Minimum, Math.Min(_maximum, value)) : Minimum;

        public static uint AdjustValue(long value, long delta, uint minimum, uint maximum)
        {
            long lower = Math.Min(minimum, maximum);
            long clamped = Math.Max(lower, Math.Min(maximum, value));
            if (delta >= 0) return (uint)(clamped + Math.Min(delta, maximum - clamped));
            return delta < lower - clamped ? (uint)lower : (uint)(clamped + delta);
        }

        private void Adjust(int delta)
        {
            if (!_interactable || !_input.interactable) return;
            _input.text = AdjustValue(Value, delta, (uint)Minimum, _maximum).ToString();
        }

        private void RefreshButtons()
        {
            if (_input == null) return;
            SetButton(_minusTen, -_largeStep, Value > Minimum);
            SetButton(_minusOne, -_smallStep, Value > Minimum);
            SetButton(_plusOne, _smallStep, Value < _maximum);
            SetButton(_plusTen, _largeStep, Value < _maximum);
        }

        private void SetButton(Button button, int delta, bool available)
        {
            if (button == null) return;
            button.interactable = _interactable && _input.interactable && available;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            long magnitude = Math.Abs((long)delta);
            if (label != null) label.text = (delta > 0 ? "+" : "-") +
                (magnitude >= 1000 && magnitude % 1000 == 0 ? (magnitude / 1000) + "K" : magnitude.ToString());
        }
    }
}
