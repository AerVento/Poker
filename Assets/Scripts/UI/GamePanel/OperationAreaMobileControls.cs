using System;
using Game.Input;
using Game.Network;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.GamePanelElement
{
    public sealed class OperationAreaMobileControls : MonoBehaviour
    {
        [Header("Existing controls")]
        [SerializeField] private Button _checkButton;
        [SerializeField] private Button _callButton;
        [SerializeField] private Button _raiseButton;
        [SerializeField] private Button _allInButton;
        [SerializeField] private Button _foldButton;
        [SerializeField] private TMP_InputField _raiseInput;

        [Header("Touch amount controls")]
        [SerializeField] private Button _decreaseBlindButton;
        [SerializeField] private Button _increaseBlindButton;
        [SerializeField] private TMP_Text _callCost;
        [SerializeField] private Button _minimumButton;
        [SerializeField] private Button _halfPotButton;
        [SerializeField] private Button _potButton;

        [Header("Shortcut labels")]
        [SerializeField] private TMP_Text _checkShortcut;
        [SerializeField] private TMP_Text _callShortcut;
        [SerializeField] private TMP_Text _raiseShortcut;
        [SerializeField] private TMP_Text _allInShortcut;
        [SerializeField] private TMP_Text _foldShortcut;

        private ClientGameInfo _info;
        private uint _bigBlind;
        private string _shortcutSignature;

        private ulong AvailableTotal => _info.IsSpectator ? 0UL : Math.Min(uint.MaxValue,
            (ulong)_info.CurrentPlayer.StageBet + _info.CurrentPlayer.Chips);

        private uint MinimumBetTotal => (uint)Math.Min(AvailableTotal,
            (ulong)_info.CurrentHighestPlayerBet + _info.LeastRaise);

        private void OnEnable()
        {
            _decreaseBlindButton?.onClick.AddListener(DecreaseBlind);
            _increaseBlindButton?.onClick.AddListener(IncreaseBlind);
            if (_minimumButton != null)
                _minimumButton.onClick.AddListener(SelectMinimum);
            if (_halfPotButton != null)
                _halfPotButton.onClick.AddListener(SelectHalfPot);
            if (_potButton != null)
                _potButton.onClick.AddListener(SelectPot);
            RefreshShortcutLabels();
        }

        private void OnDisable()
        {
            _decreaseBlindButton?.onClick.RemoveListener(DecreaseBlind);
            _increaseBlindButton?.onClick.RemoveListener(IncreaseBlind);
            if (_minimumButton != null)
                _minimumButton.onClick.RemoveListener(SelectMinimum);
            if (_halfPotButton != null)
                _halfPotButton.onClick.RemoveListener(SelectHalfPot);
            if (_potButton != null)
                _potButton.onClick.RemoveListener(SelectPot);
        }

        private void LateUpdate() => RefreshShortcutLabels();

        public void Present(ClientGameInfo info, uint bigBlind = 0)
        {
            _info = info;
            _bigBlind = bigBlind > 0 ? bigBlind : (uint)Math.Max(1L, 2L * (info.RoomInfo?.SmallBlindChips ?? 1));
            bool canChooseAmount = info.RoomInfo != null && !info.IsSpectator && info.CanRaise &&
                _raiseInput != null && _raiseInput.interactable && _raiseInput.gameObject.activeInHierarchy && AvailableTotal >= MinimumBetTotal;

            SetQuickButtonState(_decreaseBlindButton, canChooseAmount);
            SetQuickButtonState(_increaseBlindButton, canChooseAmount);
            if (_callCost != null)
            {
                var needed = info.CurrentHighestPlayerBet > info.CurrentPlayer.StageBet
                    ? info.CurrentHighestPlayerBet - info.CurrentPlayer.StageBet : 0;
                _callCost.text = "$" + ChipAmountFormatter.Format(Math.Min(needed, info.CurrentPlayer.Chips));
            }

            SetQuickButtonState(_minimumButton, canChooseAmount);
            SetQuickButtonState(_halfPotButton, canChooseAmount);
            SetQuickButtonState(_potButton, canChooseAmount);
        }

        public void TryCheckOrCall()
        {
            if (!TryClick(_checkButton))
                TryClick(_callButton);
        }

        public void TryFold() => TryClick(_foldButton);

        public void TryAllIn() => TryClick(_allInButton);

        public void TryRaise() => TryClick(_raiseButton);

        public void FocusRaiseAmount()
        {
            if (_raiseInput == null || !_raiseInput.isActiveAndEnabled || !_raiseInput.interactable)
                return;
            EventSystem.current?.SetSelectedGameObject(_raiseInput.gameObject);
            _raiseInput.Select();
            _raiseInput.ActivateInputField();
        }

        private void DecreaseBlind() => AdjustBlind(false);

        private void IncreaseBlind() => AdjustBlind(true);

        private void AdjustBlind(bool increase)
        {
            var current = uint.TryParse(_raiseInput.text, out var parsed) ? parsed : MinimumBetTotal;
            var desired = increase ? Math.Min(uint.MaxValue, (ulong)current + _bigBlind)
                : current > _bigBlind ? current - _bigBlind : 0UL;
            SetAmount((uint)desired);
        }

        private void SelectMinimum() => SetAmount(MinimumBetTotal);

        private void SelectHalfPot() => SetPotFraction(0.5f);

        private void SelectPot() => SetPotFraction(1f);

        private void SetPotFraction(float fraction)
        {
            if (_info.RoomInfo == null || _info.IsSpectator)
                return;
            ulong desired = (ulong)_info.CurrentPlayer.StageBet + (ulong)Math.Ceiling(_info.TotalPot * fraction);
            SetAmount((uint)Math.Min(AvailableTotal, Math.Max(MinimumBetTotal, desired)));
        }

        private void SetAmount(uint amount)
        {
            if (_raiseInput == null || !_raiseInput.isActiveAndEnabled || !_raiseInput.interactable)
                return;
            var corrected = (uint)Math.Min(AvailableTotal, Math.Max(MinimumBetTotal, amount));
            _raiseInput.SetTextWithoutNotify(corrected.ToString());
            _raiseInput.onEndEdit.Invoke(_raiseInput.text);
            Present(_info, _bigBlind);
        }

        private static bool TryClick(Button button)
        {
            if (button == null || !button.isActiveAndEnabled || !button.interactable)
                return false;
            button.onClick.Invoke();
            return true;
        }

        private static void SetQuickButtonState(Button button, bool interactable)
        {
            if (button != null)
            {
                button.gameObject.SetActive(interactable);
                button.interactable = interactable;
            }
        }

        private void RefreshShortcutLabels()
        {
            var signature = string.Join("|",
                PokerShortcutBindings.GetDisplayName(PokerShortcutAction.CheckOrCall),
                PokerShortcutBindings.GetDisplayName(PokerShortcutAction.FocusRaiseAmount),
                PokerShortcutBindings.GetDisplayName(PokerShortcutAction.Confirm),
                PokerShortcutBindings.GetDisplayName(PokerShortcutAction.AllIn),
                PokerShortcutBindings.GetDisplayName(PokerShortcutAction.Fold));
            if (_shortcutSignature == signature)
                return;
            _shortcutSignature = signature;
            SetShortcut(_checkShortcut, PokerShortcutAction.CheckOrCall);
            SetShortcut(_callShortcut, PokerShortcutAction.CheckOrCall);
            if (_raiseShortcut != null)
            {
                _raiseShortcut.text = $"{PokerShortcutBindings.GetDisplayName(PokerShortcutAction.FocusRaiseAmount)} 输入 · " +
                    $"{PokerShortcutBindings.GetDisplayName(PokerShortcutAction.Confirm)} 确认";
            }
            SetShortcut(_allInShortcut, PokerShortcutAction.AllIn);
            SetShortcut(_foldShortcut, PokerShortcutAction.Fold);
        }

        private static void SetShortcut(TMP_Text label, PokerShortcutAction action)
        {
            if (label != null)
                label.text = PokerShortcutBindings.GetDisplayName(action);
        }
    }
}
