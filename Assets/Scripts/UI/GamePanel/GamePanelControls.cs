using Framework.UI;
using Game.Input;
using Game.Network;
using Game.UI.GamePanelElement;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class GamePanelControls : MonoBehaviour
    {
        [SerializeField] private GamePanel _panel;
        [SerializeField] private SwitchControl _spectatorSwitch;
        [SerializeField] private TMP_Text _spectatorCount;
        [SerializeField] private Button _dailyTasksButton;
        [SerializeField] private Button _exchangeButton;
        [SerializeField] private Button _historyButton;
        [SerializeField] private TMP_Text _dollars;
        [SerializeField] private TMP_Text _chips;
        [SerializeField] private PanelShortcutHandler _shortcuts;
        [SerializeField] private OperationAreaMobileControls _mobileOperations;

        private NetworkRoom _network;

        private void Awake()
        {
            if (_panel == null)
                _panel = GetComponent<GamePanel>();
            if (_mobileOperations == null)
                _mobileOperations = GetComponentInChildren<OperationAreaMobileControls>(true);
        }

        private void OnEnable()
        {
            if (_spectatorSwitch != null)
            {
                _spectatorSwitch.SetIsOnWithoutNotify(_panel != null && _panel.ShowSpectators);
                _spectatorSwitch.ValueChanged += ShowSpectators;
            }
            if (_dailyTasksButton != null)
                _dailyTasksButton.onClick.AddListener(OpenDailyTasks);
            if (_exchangeButton != null)
                _exchangeButton.onClick.AddListener(OpenExchange);
            if (_historyButton != null)
                _historyButton.onClick.AddListener(OpenHistory);
            if (_shortcuts != null)
                _shortcuts.ShortcutTriggered += HandleShortcut;

            _network = NetworkManager.singleton as NetworkRoom;
            if (_network != null)
                _network.OnRoundResultChanged += RoundResultChanged;
            RefreshHistory();
        }

        private void OnDisable()
        {
            if (_spectatorSwitch != null)
                _spectatorSwitch.ValueChanged -= ShowSpectators;
            if (_dailyTasksButton != null)
                _dailyTasksButton.onClick.RemoveListener(OpenDailyTasks);
            if (_exchangeButton != null)
                _exchangeButton.onClick.RemoveListener(OpenExchange);
            if (_historyButton != null)
                _historyButton.onClick.RemoveListener(OpenHistory);
            if (_shortcuts != null)
                _shortcuts.ShortcutTriggered -= HandleShortcut;
            if (_network != null)
                _network.OnRoundResultChanged -= RoundResultChanged;
            _network = null;
        }

        public void Present(ClientGameInfo info, GameInfoSync sync)
        {
            int spectators = info.RoomInfo?.Spectators?.Count ?? 0;
            if (_spectatorCount != null)
                _spectatorCount.text = $"观战 {spectators}";
            if (_spectatorSwitch != null)
                _spectatorSwitch.gameObject.SetActive(spectators > 0);
            if (_dailyTasksButton != null)
                _dailyTasksButton.interactable = _network?.CanOpenDailyTasks == true;
            if (_exchangeButton != null)
                _exchangeButton.interactable = _network?.CanOpenWallet == true;
            _mobileOperations?.Present(info, sync.BigBlind);
            RefreshHistory();
        }

        public void PresentWallet(uint dollars, uint chips)
        {
            if (_dollars != null)
                _dollars.text = $"${ChipAmountFormatter.Format(dollars)}";
            if (_chips != null)
                _chips.text = $"${ChipAmountFormatter.Format(chips)}";
        }

        private void ShowSpectators(bool show) => _panel?.SetShowSpectators(show);

        private void OpenDailyTasks()
        {
            if (_network?.CanOpenDailyTasks == true)
                UIManager.Instance.PushPanel<DailyTaskPanel>(UIManager.Layer.System);
        }

        private void OpenExchange()
        {
            if (_network?.CanOpenWallet == true)
                UIManager.Instance.PushPanel<ChipExchangePanel>(UIManager.Layer.System);
        }

        private void OpenHistory()
        {
            var result = _network?.LastRoundResult;
            if (result == null)
                return;
            var panel = UIManager.Instance.PushPanel<RoundResultMsgBox>(UIManager.Layer.System);
            panel?.SetResult(result, false);
        }

        private void RoundResultChanged(RoundResult result, bool automatic)
        {
            RefreshHistory();
            if (result == null)
                return;
            if (!automatic && RoundResultMsgBox.Instance != null && RoundResultMsgBox.Instance.IsShowing)
                RoundResultMsgBox.Instance.SetResult(result, false);
        }

        private void RefreshHistory()
        {
            if (_historyButton != null)
                _historyButton.gameObject.SetActive(_network?.LastRoundResult != null);
        }

        private void HandleShortcut(PokerShortcutAction action)
        {
            switch (action)
            {
                case PokerShortcutAction.Confirm:
                    _mobileOperations?.TryRaise();
                    break;
                case PokerShortcutAction.Fold:
                    _mobileOperations?.TryFold();
                    break;
                case PokerShortcutAction.CheckOrCall:
                    _mobileOperations?.TryCheckOrCall();
                    break;
                case PokerShortcutAction.FocusRaiseAmount:
                    _mobileOperations?.FocusRaiseAmount();
                    break;
                case PokerShortcutAction.AllIn:
                    _mobileOperations?.TryAllIn();
                    break;
            }
        }
    }
}
