using Framework.UI;
using Game.Network;
using Game.UI.GamePanelElement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class GamePanel : SingletonPanel<GamePanel>
    {
        [SerializeField]
        private TextMeshProUGUI _title;

        [SerializeField]
        private TextMeshProUGUI _accountStatus;

        [SerializeField]
        private TextMeshProUGUI _tableStatus;

        [SerializeField]
        private TextMeshProUGUI _countdownStatus;

        [SerializeField]
        private GameObject _playerPrefab;
        
        [SerializeField]
        private Transform _playerList;
        
        [SerializeField]
        private OperationArea _operationArea;

        private Button _leaveButton;

        private TextMeshProUGUI _leaveButtonLabel;

        [SerializeField]
        private TextMeshProUGUI _leaveStatus;

        private Button _participationButton;

        private TextMeshProUGUI _participationButtonLabel;

        private Button _keepSpectatingButton;

        [SerializeField]
        private Button _settingsButton;

        private GameInfoSync _sync;
        private bool _participationRequestInFlight;
        private bool _canLeaveImmediately;
        private bool _leaveAfterHand;
        private bool _leaveRequestInFlight;
        private List<PlayerListItem> _playerListItems;

        private ClientGameInfo _info;
        private uint _accountChips;
        private uint _bigBlind;
        private TableParticipationStatus _participationStatus;
        public ClientGameInfo Info
        {
            get => _info;
            set
            {
                if (_playerListItems != null && (_info.RoomInfo == null || value.RoomInfo == null ||
                    _info.RoomInfo.Id != value.RoomInfo.Id || _info.Round != value.Round))
                {
                    foreach (var item in _playerListItems)
                        item.IsOperating = false;
                }
                _info = value;
                Refresh();
            }
        }

        public void ApplySync(GameInfoSync message)
        {
            _sync = message;
            _accountChips = message.AccountChips;
            _bigBlind = message.BigBlind;
            _participationStatus = message.ParticipationStatus;
            _canLeaveImmediately = message.CanLeaveImmediately;
            _leaveAfterHand = message.LeaveAfterHand;
            Info = message.Info;
            GameAudioManager.EnsureClient()?.ObserveGame(message.Info);
            if (_info.Round != GameStage.RoundOver)
                SetInterHandCountdown(false, 0);
        }

        private void OpenSettings()
        {
            UIManager.Instance.PushPanel<InGameOptionPanel>();
        }

        private void OnEnable()
        {
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OpenSettings);

        }

        private void OnDisable()
        {
            if (_settingsButton != null)
                _settingsButton.onClick.RemoveListener(OpenSettings);
            if (UIManager.Available && UIManager.Instance.ContainsPanel<InGameOptionPanel>())
                UIManager.Instance.PopPanel<InGameOptionPanel>();
            DetachSettingsControls();
            _participationRequestInFlight = false;
            _leaveRequestInFlight = false;
            SetInterHandCountdown(false, 0);
            if (_playerListItems != null)
            {
                foreach (var player in _playerListItems)
                    player.IsOperating = false;
            }
        }

        // Settings owns the controls; the live game retains request and authoritative state handling.
        public void AttachSettingsControls(Button leave, TextMeshProUGUI leaveLabel,
            Button participation, TextMeshProUGUI participationLabel, Button keepSpectating)
        {
            DetachSettingsControls();
            _leaveButton = leave;
            _leaveButtonLabel = leaveLabel;
            _participationButton = participation;
            _participationButtonLabel = participationLabel;
            _keepSpectatingButton = keepSpectating;
            _leaveButton.onClick.AddListener(LeaveRoom);
            _participationButton.onClick.AddListener(ChangeParticipation);
            _keepSpectatingButton.onClick.AddListener(KeepSpectating);
            RefreshLeaveControls();
            RefreshParticipationControls();
        }

        public void DetachSettingsControls()
        {
            if (_leaveButton != null)
                _leaveButton.onClick.RemoveListener(LeaveRoom);
            if (_participationButton != null)
                _participationButton.onClick.RemoveListener(ChangeParticipation);
            if (_keepSpectatingButton != null)
                _keepSpectatingButton.onClick.RemoveListener(KeepSpectating);
            _leaveButton = null;
            _leaveButtonLabel = null;
            _participationButton = null;
            _participationButtonLabel = null;
            _keepSpectatingButton = null;
        }

        public void SetLeaveRequestInFlight(bool inFlight)
        {
            _leaveRequestInFlight = inFlight;
            RefreshLeaveControls();
            RefreshParticipationControls();
        }

        public void SetLeaveAfterHand(bool leaveAfterHand)
        {
            _leaveAfterHand = leaveAfterHand;
            RefreshLeaveControls();
            RefreshParticipationControls();
        }

        private void LeaveRoom()
        {
            if (_info.RoomInfo == null || _leaveRequestInFlight)
                return;

            int roomId = _info.RoomInfo.Id;
            if (_leaveAfterHand || _canLeaveImmediately)
            {
                SendLeaveRequest(roomId, !_canLeaveImmediately && _leaveAfterHand);
                return;
            }

            var handId = _info.HandId;
            MsgBox.Create("LeaveRoom", msgbox =>
            {
                msgbox.Type = MsgboxType.ConfirmAndCancel;
                msgbox.Title = "本手结束后离开";
                msgbox.Message = "本手结算并查看结果后、下一手准备时，将自动返回大厅。期间可正常操作或取消预约。";
                msgbox.ConfirmButtonText = "预约离开";
                msgbox.CancelButtonText = "继续游戏";
                msgbox.OnConfirm += () => SendLeaveRequest(roomId, false, handId);
            });
        }

        private void SendLeaveRequest(int roomId, bool cancelPending, string expectedHandId = null)
        {
            if (!isActiveAndEnabled || _info.RoomInfo?.Id != roomId || _leaveRequestInFlight)
                return;

            SetLeaveRequestInFlight(true);
            if (!(Mirror.NetworkManager.singleton is NetworkRoom networkRoom) ||
                !networkRoom.RequestLeaveRoom(roomId, cancelPending, expectedHandId ?? _info.HandId,
                    expectedHandId == null && _canLeaveImmediately))
                SetLeaveRequestInFlight(false);
        }

        private void RefreshLeaveControls()
        {
            if (_leaveButton != null)
                _leaveButton.interactable = _info.RoomInfo != null && !_leaveRequestInFlight;
            if (_leaveButtonLabel != null)
                _leaveButtonLabel.text = _leaveRequestInFlight ? "处理中…" :
                    _canLeaveImmediately ? "离开房间" :
                    _leaveAfterHand ? "取消离开" : "本手后离开";
            if (_leaveStatus != null)
            {
                _leaveStatus.gameObject.SetActive(_leaveAfterHand);
                _leaveStatus.text = _leaveAfterHand ? "查看结果结束后返回大厅" : string.Empty;
            }
        }

        public void SetParticipationRequestInFlight(bool value)
        {
            _participationRequestInFlight = value;
            RefreshParticipationControls();
        }

        private void ChangeParticipation()
        {
            if (_info.IsSpectator)
            {
                if (_sync.CanJoinNextHand && !_sync.JoinNextHand)
                    SendParticipation(TableParticipationAction.JoinNextHand);
            }
            else
            {
                SendParticipation(!_canLeaveImmediately && _sync.SitOutAfterHand
                    ? TableParticipationAction.CancelSitOut
                    : TableParticipationAction.SitOutAfterHand);
            }
        }

        private void KeepSpectating()
        {
            if (_info.IsSpectator)
                SendParticipation(TableParticipationAction.KeepSpectating);
            else if (_leaveAfterHand)
                SendLeaveRequest(_info.RoomInfo.Id, true);
            else if (_sync.SitOutAfterHand)
                SendParticipation(TableParticipationAction.CancelSitOut);
        }

        private void SendParticipation(TableParticipationAction action)
        {
            if (!isActiveAndEnabled || _info.RoomInfo == null || _participationRequestInFlight)
                return;
            SetParticipationRequestInFlight(true);
            if (!(Mirror.NetworkManager.singleton is NetworkRoom room) || !room.RequestTableParticipation(_sync, action))
                SetParticipationRequestInFlight(false);
        }

        private void RefreshParticipationControls()
        {
            if (_participationButton == null || _participationButtonLabel == null)
                return;
            var spectator = _info.IsSpectator;
            _participationButton.gameObject.SetActive(true);
            _participationButton.interactable = _info.RoomInfo != null && !_participationRequestInFlight &&
                (_canLeaveImmediately || !_leaveAfterHand) && (!spectator || (_sync.CanJoinNextHand && !_sync.JoinNextHand));
            _participationButtonLabel.text = _participationRequestInFlight ? "处理中…" :
                spectator ? (_sync.JoinNextHand ? "已申请下一手" :
                    _sync.CanJoinNextHand ? "下一手参战" : _accountChips < _bigBlind ? "筹码不足" : "参战名额已满") :
                _canLeaveImmediately ? "转为旁观" : _sync.SitOutAfterHand ? "取消转旁观" : "本手后旁观";
            if (_keepSpectatingButton != null)
            {
                _keepSpectatingButton.gameObject.SetActive(spectator ||
                    (_canLeaveImmediately && (_leaveAfterHand || _sync.SitOutAfterHand)));
                _keepSpectatingButton.interactable = !_participationRequestInFlight && !_leaveRequestInFlight;
                var label = _keepSpectatingButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = spectator ? "继续旁观" : _leaveAfterHand ? "取消离开预约" : "取消观战预约";
            }
        }

        public void OnOperationRejected() => _operationArea.OnOperationRejected();

        public void SetAccountChips(uint chips)
        {
            _accountChips = chips;
            if (_accountStatus != null)
                _accountStatus.text = $"账户余额  ${chips:N0}";
        }

        public void SetInterHandCountdown(bool isActive, int secondsRemaining, bool canStartNextHand = true)
        {
            if (_countdownStatus == null)
                return;

            _countdownStatus.gameObject.SetActive(isActive);
            _countdownStatus.text = isActive
                ? canStartNextHand ? $"下一手将在 {secondsRemaining} 秒后开始"
                    : $"当前可参战人数不足，{secondsRemaining} 秒后返回"
                : string.Empty;
        }

        private void Refresh()
        {
            if (_info.RoomInfo == null)
                return;
            if (_playerListItems == null)
                _playerListItems = new List<PlayerListItem>();
            while (_playerListItems.Count < _info.PlayersCount)
            {
                var obj = Instantiate(_playerPrefab, _playerList);
                _playerListItems.Add(obj.GetComponent<PlayerListItem>());
            }
            for (int i = _info.PlayersCount; i < _playerListItems.Count; i++)
                _playerListItems[i].gameObject.SetActive(false);

            // 更新Player
            for(int i = 0; i < _info.PlayersCount; i++)
            {
                var listItem = _playerListItems[i];
                var playerInfo = _info.Players[i];
                listItem.gameObject.SetActive(playerInfo.IsOccupied);
                if (!playerInfo.IsOccupied)
                    continue;
                if (listItem.PlayerName != playerInfo.PlayerName)
                    listItem.IsOperating = false;
                listItem.ThinkingTime = _info.RoomInfo.ThinkingTime;
                listItem.IsDealer = i == _info.Dealer;
                listItem.IsSmallBlind = i == _info.SmallBlind;
                listItem.IsBigBlind = i == _info.BigBlind;
                listItem.IsOperating = i == _info.OperatingPlayer && _info.Round < GameStage.RoundOver;
                listItem.IsFold = playerInfo.IsFold;
                listItem.IsDisconnected = playerInfo.IsDisconnected;
                listItem.PlayerName = playerInfo.PlayerName;
                listItem.Chip = playerInfo.Chips;
                listItem.Operation = playerInfo.LastOperation;
            }

            // 更新操作区
            _operationArea.Info = _info;

            RefreshLeaveControls();
            RefreshParticipationControls();

            // 更新标题
            var stage = _info.Round switch
            {
                GameStage.Preflop => "翻牌前",
                GameStage.Flop => "翻牌",
                GameStage.Turn => "转牌",
                GameStage.River => "河牌",
                GameStage.RoundOver => "本手结束",
                _ => "牌局结束",
            };
            _title.text = $"房间 {_info.RoomInfo.Id} · {stage} · 奖池 ${BigNumberToString(_info.TotalPot)}";

            if (_accountStatus != null)
                _accountStatus.text = $"账户余额  ${_accountChips:N0}";
            if (_tableStatus != null)
            {
                _tableStatus.text = _leaveAfterHand ? string.Empty : _participationStatus switch
                {
                    TableParticipationStatus.WaitingForNextHand => "等待下一手",
                    TableParticipationStatus.Spectating => "旁观中",
                    TableParticipationStatus.InsufficientChips => $"筹码不足（至少需要 ${_bigBlind:N0}）",
                    _ => _sync.SitOutAfterHand ? "已预约：查看结果结束后转为旁观" : string.Empty,
                };
            }
        }


        private string BigNumberToString(uint number)
        {
            if (number < 1000)
                return number.ToString();
            else if (number < 1000000)
                return System.MathF.Round((float)number / 1000, 1).ToString() + "K";
            else if (number < 1000000000)
                return System.MathF.Round((float)number / 1000000, 1).ToString() + "M";
            else
                return System.MathF.Round((float)number / 1000000000, 1).ToString() + "B";
        }
    }
}
