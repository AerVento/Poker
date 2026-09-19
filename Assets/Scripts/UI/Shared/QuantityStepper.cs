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
        private uint _maximum = uint.MaxValue;

        public void Configure(uint maximum) => _maximum = Math.Max(1u, maximum);

        private void OnEnable()
        {
            _minusTen.onClick.AddListener(MinusTen);
            _minusOne.onClick.AddListener(MinusOne);
            _plusOne.onClick.AddListener(PlusOne);
            _plusTen.onClick.AddListener(PlusTen);
        }

        private void OnDisable()
        {
            _minusTen.onClick.RemoveListener(MinusTen);
            _minusOne.onClick.RemoveListener(MinusOne);
            _plusOne.onClick.RemoveListener(PlusOne);
            _plusTen.onClick.RemoveListener(PlusTen);
        }

        private void MinusTen() => Adjust(-10);
        private void MinusOne() => Adjust(-1);
        private void PlusOne() => Adjust(1);
        private void PlusTen() => Adjust(10);

        private void Adjust(int delta)
        {
            if (!long.TryParse(_input.text, out var value)) value = 1;
            _input.text = Math.Max(1L, Math.Min(_maximum, value + delta)).ToString();
        }
    }
}
