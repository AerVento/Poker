using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class InventoryItemCard : MonoBehaviour
    {
        [SerializeField] private Button _select;
        [SerializeField] private Image _icon;
        [Tooltip("Drawn placeholder shown until the item's remote icon is available.")]
        [SerializeField] private GameObject[] _placeholder = Array.Empty<GameObject>();
        [SerializeField] private Image _background;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _kind;
        [SerializeField] private TextMeshProUGUI _quantity;
        private Action _onSelect;

        private void OnEnable() => _select.onClick.AddListener(Select);
        private void OnDisable()
        {
            _select.onClick.RemoveListener(Select);
            _onSelect = null;
        }

        public void Present(string name, string kind, Sprite icon, string quantity, bool selected, Action onSelect)
        {
            _name.text = name;
            _kind.text = kind;
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            foreach (var part in _placeholder) if (part != null) part.SetActive(icon == null);
            _quantity.text = quantity;
            _background.color = selected ? new Color(0.20f, 0.31f, 0.25f) : new Color(0.065f, 0.14f, 0.115f);
            _onSelect = onSelect;
        }

        private void Select() => _onSelect?.Invoke();
    }
}
