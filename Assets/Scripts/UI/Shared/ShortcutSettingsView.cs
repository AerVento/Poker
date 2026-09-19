using System;
using System.Collections.Generic;
using Game.Input;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class ShortcutSettingsView : MonoBehaviour
    {
        [Serializable]
        public sealed class BindingRow
        {
            [SerializeField] private PokerShortcutAction _shortcut;
            [SerializeField] private Button _rebindButton;
            [SerializeField] private TextMeshProUGUI _bindingLabel;

            public PokerShortcutAction Shortcut => _shortcut;
            public Button RebindButton => _rebindButton;
            public TextMeshProUGUI BindingLabel => _bindingLabel;
        }

        [SerializeField] private BindingRow[] _rows;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _cancelRebindButton;
        [SerializeField] private GameObject _rebindPrompt;
        [SerializeField] private TextMeshProUGUI _status;

        private readonly Dictionary<Button, UnityAction> _buttonListeners = new();
        private bool _ownsRebind;

        private void OnEnable()
        {
            PokerShortcutBindings.EnsureInitialized();
            PokerShortcutBindings.BindingChanged += Refresh;
            RegisterButtons();
            Refresh();
            SetStatus(string.Empty);
            SetRebindState(false);
        }

        private void OnDisable()
        {
            PokerShortcutBindings.BindingChanged -= Refresh;
            UnregisterButtons();
            if (_ownsRebind)
                PokerShortcutBindings.CancelInteractiveRebind();
            _ownsRebind = false;
        }

        private void RegisterButtons()
        {
            if (_rows != null)
            {
                foreach (var row in _rows)
                {
                    if (row?.RebindButton == null)
                        continue;
                    var capturedRow = row;
                    UnityAction listener = () => BeginRebind(capturedRow);
                    row.RebindButton.onClick.AddListener(listener);
                    _buttonListeners[row.RebindButton] = listener;
                }
            }

            AddButtonListener(_resetButton, ResetDefaults);
            AddButtonListener(_cancelRebindButton, CancelRebind);
        }

        private void AddButtonListener(Button button, UnityAction listener)
        {
            if (button == null)
                return;
            button.onClick.AddListener(listener);
            _buttonListeners[button] = listener;
        }

        private void UnregisterButtons()
        {
            foreach (var pair in _buttonListeners)
            {
                if (pair.Key != null)
                    pair.Key.onClick.RemoveListener(pair.Value);
            }
            _buttonListeners.Clear();
        }

        private void BeginRebind(BindingRow row)
        {
            if (row == null || PokerShortcutBindings.IsRebinding)
                return;

            _ownsRebind = true;
            SetRebindState(true);
            SetStatus("请按下新按键（Esc 取消）");
            if (!PokerShortcutBindings.TryBeginInteractiveRebind(row.Shortcut, OnRebindCompleted))
            {
                _ownsRebind = false;
                SetRebindState(false);
            }
        }

        private void OnRebindCompleted(ShortcutRebindOutcome outcome)
        {
            _ownsRebind = false;
            if (this == null)
                return;
            SetRebindState(false);
            Refresh();
            SetStatus(outcome.Result switch
            {
                ShortcutRebindResult.Success => "快捷键已更新。",
                ShortcutRebindResult.Cancelled => "已取消改键。",
                ShortcutRebindResult.Conflict =>
                    $"与“{PokerShortcutBindings.GetActionLabel(outcome.ConflictingAction)}”冲突。",
                ShortcutRebindResult.InvalidControl => "请选择普通键盘按键。",
                ShortcutRebindResult.Busy => "另一个快捷键正在修改。",
                _ => string.Empty,
            });
        }

        private void CancelRebind()
        {
            if (_ownsRebind)
                PokerShortcutBindings.CancelInteractiveRebind();
        }

        private void ResetDefaults()
        {
            PokerShortcutBindings.ResetDefaults();
            SetStatus("已恢复默认快捷键。");
        }

        private void Refresh()
        {
            if (_rows == null)
                return;
            foreach (var row in _rows)
            {
                if (row == null)
                    continue;
                if (row.BindingLabel != null)
                    row.BindingLabel.text = PokerShortcutBindings.GetDisplayName(row.Shortcut);
                if (row.RebindButton != null)
                    row.RebindButton.interactable = !PokerShortcutBindings.IsRebinding;
            }
        }

        private void SetRebindState(bool rebinding)
        {
            if (_rebindPrompt != null)
                _rebindPrompt.SetActive(rebinding);
            if (_cancelRebindButton != null)
                _cancelRebindButton.gameObject.SetActive(rebinding);
            if (_resetButton != null)
                _resetButton.interactable = !rebinding;
            Refresh();
        }

        private void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message;
        }
    }
}
