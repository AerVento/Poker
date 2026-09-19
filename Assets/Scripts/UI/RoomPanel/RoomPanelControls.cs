using Framework.UI;
using Game.Network;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class RoomPanelControls : MonoBehaviour
    {
        [SerializeField] private RoomPanel _panel;
        [SerializeField] private Button _participationButton;
        [SerializeField] private TMP_Text _participationLabel;
        [SerializeField] private Button _readyButton;
        [SerializeField] private Button _cancelReadyButton;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _dailyTasksButton;
        [SerializeField] private Button _achievementsButton;
        [SerializeField] private Button _exchangeButton;
        [SerializeField] private TMP_Text _dollars;
        [SerializeField] private TMP_Text _chips;

        private NetworkRoom _network;
        private bool _requestPending;
        private float _requestDeadline;
        private int _lastRoomId = -1;
        private bool _lastSpectator;

        private void Awake()
        {
            if (_panel == null)
                _panel = GetComponent<RoomPanel>();
        }

        private void OnEnable()
        {
            _network = NetworkManager.singleton as NetworkRoom;
            if (_participationButton != null)
                _participationButton.onClick.AddListener(ChangeParticipation);
            if (_dailyTasksButton != null)
                _dailyTasksButton.onClick.AddListener(OpenDailyTasks);
            if (_achievementsButton != null)
                _achievementsButton.onClick.AddListener(OpenAchievements);
            if (_exchangeButton != null)
                _exchangeButton.onClick.AddListener(OpenExchange);
            Refresh();
        }

        private void OnDisable()
        {
            if (_participationButton != null)
                _participationButton.onClick.RemoveListener(ChangeParticipation);
            if (_dailyTasksButton != null)
                _dailyTasksButton.onClick.RemoveListener(OpenDailyTasks);
            if (_achievementsButton != null)
                _achievementsButton.onClick.RemoveListener(OpenAchievements);
            if (_exchangeButton != null)
                _exchangeButton.onClick.RemoveListener(OpenExchange);
            _network = null;
            _requestPending = false;
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (_panel == null)
                return;
            var room = _panel.RoomInfo;
            if (room == null)
                return;
            bool spectator = _panel.IsSpectator;
            if (_lastRoomId != room.Id || _lastSpectator != spectator)
            {
                _requestPending = false;
                _lastRoomId = room.Id;
                _lastSpectator = spectator;
            }
            if (_requestPending && Time.unscaledTime >= _requestDeadline)
                _requestPending = false;

            uint chips = GameController.Available ? GameController.Instance.State.Chips : 0;
            uint bigBlind = room.SmallBlindChips > 0 ? checked((uint)room.SmallBlindChips * 2U) : 0;
            bool canJoin = !spectator || chips >= bigBlind && room.PlayerCount < room.MaxPlayerCount;

            if (_participationButton != null)
            {
                _participationButton.gameObject.SetActive(room.Id >= 0 && room.IsWaiting);
                _participationButton.interactable = room.Id >= 0 && room.IsWaiting && !_requestPending && canJoin;
            }
            if (_participationLabel != null)
                _participationLabel.text = _requestPending ? "处理中…" : spectator ?
                    chips < bigBlind ? "筹码不足" : room.PlayerCount >= room.MaxPlayerCount ? "参战名额已满" : "加入参战" :
                    "转为观战";

            if (spectator)
            {
                if (_readyButton != null)
                    _readyButton.gameObject.SetActive(false);
                if (_cancelReadyButton != null)
                    _cancelReadyButton.gameObject.SetActive(false);
                if (_startButton != null)
                    _startButton.gameObject.SetActive(false);
            }

            if (_dailyTasksButton != null)
                _dailyTasksButton.interactable = _network?.CanOpenDailyTasks == true;
            if (_achievementsButton != null)
                _achievementsButton.interactable = _network?.CanViewAchievements == true;
            if (_exchangeButton != null)
                _exchangeButton.interactable = _network?.CanOpenWallet == true;

            if (GameController.Available)
            {
                var state = GameController.Instance.State;
                if (_dollars != null)
                    _dollars.text = $"${ChipAmountFormatter.Format(state.Dollars)}";
                if (_chips != null)
                    _chips.text = $"${ChipAmountFormatter.Format(state.Chips)}";
            }
        }

        private void ChangeParticipation()
        {
            if (_requestPending || _panel == null || _panel.RoomInfo == null)
                return;
            _requestPending = _network?.RequestWaitingParticipation(_panel.RoomInfo.Id, _panel.IsSpectator) == true;
            if (_requestPending)
                _requestDeadline = Time.unscaledTime + 5f;
            Refresh();
        }

        private void OpenDailyTasks()
        {
            if (_network?.CanOpenDailyTasks == true)
                UIManager.Instance.PushPanel<DailyTaskPanel>(UIManager.Layer.System);
        }

        private void OpenAchievements()
        {
            if (_network?.CanViewAchievements == true)
                UIManager.Instance.PushPanel<AchievementPanel>(UIManager.Layer.System);
        }

        private void OpenExchange()
        {
            if (_network?.CanOpenWallet == true)
                UIManager.Instance.PushPanel<ChipExchangePanel>(UIManager.Layer.System);
        }
    }
}
