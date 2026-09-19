using System;
using Framework.UI;
using Game.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class PanelShortcutHandler : MonoBehaviour
    {
        [Header("Scope")]
        [SerializeField] private BasePanel _panel;

        [Header("Standard Actions")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_InputField _amountInput;
        [SerializeField] private bool _confirmAlwaysUsesPrimary;

        [Header("Keyboard Focus Order")]
        [SerializeField] private Selectable[] _focusOrder;

        public event Action<PokerShortcutAction> ShortcutTriggered;

        private void Awake()
        {
            InitializeForPanel(_panel == null ? GetComponentInParent<BasePanel>(true) : _panel);
        }

        private void OnEnable()
        {
            PokerShortcutBindings.EnsureInitialized();
            PokerShortcutBindings.ShortcutPerformed += HandleShortcut;
        }

        private void OnDisable()
        {
            PokerShortcutBindings.ShortcutPerformed -= HandleShortcut;
        }

        public void SetConfirmButton(Button button) => _confirmButton = button;

        public void SetBackButton(Button button) => _backButton = button;

        public void SetAmountInput(TMP_InputField input) => _amountInput = input;

        public void SetFocusOrder(params Selectable[] focusOrder) => _focusOrder = focusOrder;

        public void InitializeForPanel(BasePanel panel)
        {
            if (panel != null)
                _panel = panel;
            if (_panel == null)
                return;

            if (_confirmButton == null)
                _confirmButton = FindButton("ConfirmButton");
            if (_backButton == null)
                _backButton = FindButton("BackButton") ?? FindButton("CloseButton");
            if (_focusOrder == null || _focusOrder.Length == 0)
                _focusOrder = _panel.GetComponentsInChildren<Selectable>(true);
        }

        private void HandleShortcut(PokerShortcutAction shortcut)
        {
            if (!IsTopPanel() || !PokerShortcutBindings.TryConsumeCurrentFrame())
                return;

            bool editingText = IsEditingText();
            switch (shortcut)
            {
                case PokerShortcutAction.NextField:
                    MoveFocus(1);
                    return;
                case PokerShortcutAction.PreviousField:
                    MoveFocus(-1);
                    return;
                case PokerShortcutAction.Confirm:
                    HandleConfirm();
                    return;
                case PokerShortcutAction.Back:
                    if (_backButton != null)
                    {
                        InvokeButton(_backButton);
                        return;
                    }
                    if (!PopToPreviousPanel())
                        ShortcutTriggered?.Invoke(shortcut);
                    return;
                case PokerShortcutAction.FocusRaiseAmount:
                    if (editingText)
                        return;
                    if (_amountInput != null && _amountInput.IsInteractable() && _amountInput.gameObject.activeInHierarchy)
                    {
                        _amountInput.Select();
                        _amountInput.ActivateInputField();
                    }
                    ShortcutTriggered?.Invoke(shortcut);
                    return;
                default:
                    if (!editingText)
                        ShortcutTriggered?.Invoke(shortcut);
                    return;
            }
        }

        private bool IsTopPanel()
        {
            return _panel != null && UIManager.Available &&
                !string.IsNullOrEmpty(_panel.Identifier) &&
                UIManager.Instance.TopPanelIdentifier == _panel.Identifier;
        }

        private void HandleConfirm()
        {
            var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            var selectedButton = selected == null ? null : selected.GetComponentInParent<Button>();
            if (!_confirmAlwaysUsesPrimary && selectedButton != null &&
                selectedButton.transform.IsChildOf(_panel.transform) && InvokeButton(selectedButton))
                return;
            if (!InvokeButton(_confirmButton))
                ShortcutTriggered?.Invoke(PokerShortcutAction.Confirm);
        }

        private bool IsEditingText()
        {
            var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.transform.IsChildOf(transform))
                return false;
            var input = selected.GetComponentInParent<TMP_InputField>();
            return input != null && input.isFocused;
        }

        private void MoveFocus(int direction)
        {
            if (_focusOrder == null || _focusOrder.Length == 0 || EventSystem.current == null)
                return;

            var selected = EventSystem.current.currentSelectedGameObject;
            int currentIndex = -1;
            for (int i = 0; i < _focusOrder.Length; i++)
            {
                var selectable = _focusOrder[i];
                if (selectable != null && selected != null &&
                    (selected == selectable.gameObject || selected.transform.IsChildOf(selectable.transform)))
                {
                    currentIndex = i;
                    break;
                }
            }
            if (currentIndex < 0 && direction < 0)
                currentIndex = 0;

            for (int offset = 1; offset <= _focusOrder.Length; offset++)
            {
                int index = (currentIndex + direction * offset) % _focusOrder.Length;
                if (index < 0)
                    index += _focusOrder.Length;
                var selectable = _focusOrder[index];
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.IsInteractable())
                    continue;

                var currentInput = selected == null ? null : selected.GetComponentInParent<TMP_InputField>();
                currentInput?.DeactivateInputField();
                selectable.Select();
                if (selectable is TMP_InputField input)
                    input.ActivateInputField();
                return;
            }
        }

        private static bool InvokeButton(Button button)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable())
                return false;
            button.onClick.Invoke();
            return true;
        }

        private Button FindButton(string objectName)
        {
            foreach (var button in _panel.GetComponentsInChildren<Button>(true))
            {
                if (button.name == objectName)
                    return button;
            }
            return null;
        }

        private bool PopToPreviousPanel()
        {
            if (!UIManager.Available || UIManager.Instance.PanelOrder.Count <= 1 ||
                UIManager.Instance.TopPanelIdentifier != _panel.Identifier)
                return false;
            return UIManager.Instance.PopPanel(_panel.Identifier);
        }

    }
}
