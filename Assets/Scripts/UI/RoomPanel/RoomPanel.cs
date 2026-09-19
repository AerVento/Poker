using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Framework.UI;
using UnityEngine.UI;
using TMPro;
using Game.Network;
using Mirror;
using Assets.Scripts.Network.Messages;
using Framework.Log;
using Cysharp.Threading.Tasks;
using System;
using PlayerListItem = Game.UI.RoomPanelElement.PlayerListItem;

namespace Game.UI
{
    public class RoomPanel : SingletonPanel<RoomPanel>
    {
        [SerializeField]
        private TextMeshProUGUI _title;

        [SerializeField]
        private Button _back;

        [SerializeField]
        private Button _ready;

        [SerializeField]
        private Button _cancelReady;

        [SerializeField]
        private Button _start;

        [SerializeField]
        private ScrollRect _scroll;

        [SerializeField]
        private TextMeshProUGUI _accountStatus;

        [SerializeField]
        private TextMeshProUGUI _roomStatus;

        [SerializeField]
        private TMP_InputField _smallBlindChips;

        [SerializeField]
        private TMP_InputField _thinkingTime;

        [SerializeField] private TMP_InputField _interHandWait;

        [SerializeField]
        private TMP_InputField _maxPlayer;

        [SerializeField]
        private TextMeshProUGUI _settingsHint;

        [SerializeField]
        private Transform _playerList;

        /// <summary>
        /// UI预制体
        /// </summary>
        [SerializeField]
        private GameObject _player;


        private RoomInfo _roomInfo = new RoomInfo()
        {
            Id = -1,
            MaxPlayerCount = 0,
            OwnerName = "undefined",
        };

        private List<PlayerListItem> _tmpPlayerItems = new List<PlayerListItem>();

        public RoomInfo RoomInfo
        {
            get => _roomInfo;
            set
            {
                _roomInfo = value;
                Refresh();
            }
        }

        public string SelfName => GameController.Instance.State.Nickname;
        
        /// <summary>
        /// 当前玩家是否是房主
        /// </summary>
        public bool IsOwner => SelfName == RoomInfo.OwnerName;

        public bool IsSpectator => RoomInfo.Spectators != null &&
            RoomInfo.Spectators.Contains(SelfName);

        /// <summary>
        /// 当前玩家是否已准备
        /// </summary>
        public bool IsReady
        {
            get
            {
                // 如果是房主，则默认房主已经准备
                if (IsOwner)
                    return true;

                if (IsSpectator)
                    return false;

                int index = RoomInfo.Players.IndexOf(SelfName);
                if(index == -1)
                {
                    Log.WriteError($"Error! The current player {SelfName} is not in the room {RoomInfo.Id}!");
                    Log.WriteError(new System.Diagnostics.StackTrace().ToString());
                    throw new System.IndexOutOfRangeException();
                }
                return RoomInfo.ReadyStatus[index];
            }
        }

        private void OnEnable()
        {
            GameAudioManager.EnsureClient()?.EnterLobby();
            _back.interactable = true;
            SetSettingsHint(string.Empty);
            _back.onClick.AddListener(Back);
            _ready.onClick.AddListener(ChangeReady);
            _cancelReady.onClick.AddListener(ChangeReady);
            _start.onClick.AddListener(StartGame);

            _smallBlindChips.onEndEdit.AddListener(ChangeSmallBlindChip);
            _thinkingTime.onEndEdit.AddListener(ChangeTime);
            _interHandWait.onEndEdit.AddListener(ChangeInterHandWait);
            _maxPlayer.onEndEdit.AddListener(ChangeMaxPlayer);

        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _ready.onClick.RemoveListener(ChangeReady);
            _cancelReady.onClick.RemoveListener(ChangeReady);
            _start.onClick.RemoveListener(StartGame);

            _smallBlindChips.onEndEdit.RemoveListener(ChangeSmallBlindChip);
            _thinkingTime.onEndEdit.RemoveListener(ChangeTime);
            _interHandWait.onEndEdit.RemoveListener(ChangeInterHandWait);
            _maxPlayer.onEndEdit.RemoveListener(ChangeMaxPlayer);

        }



        public void SetLeaveRequestInFlight(bool inFlight)
        {
            _back.interactable = !inFlight;
        }

        private void Back()
        {
            if (!_back.interactable)
                return;
            SetLeaveRequestInFlight(true);
            if (!(NetworkManager.singleton is NetworkRoom networkRoom) ||
                !networkRoom.RequestLeaveRoom(_roomInfo.Id))
                SetLeaveRequestInFlight(false);
        }

        private void ChangeReady()
        {
            NetworkClient.Send(new PlayerChangeReadyMessage() { PlayerName = SelfName, RoomId = _roomInfo.Id });
        }

        private void StartGame()
        {
            NetworkClient.Send(new PlayerStartGameRequest() { PlayerName = SelfName, RoomId = _roomInfo.Id });
        }

        private void ChangeSmallBlindChip(string value)
        {
            if (!System.Int32.TryParse(value, out var result) || result <= 0)
            {
                _smallBlindChips.SetTextWithoutNotify(_roomInfo.SmallBlindChips.ToString());
                SetSettingsHint("小盲须为 1 至 2,147,483,647 的整数，已恢复原值。");
                return;
            }
            // 防止筹码量过大时溢出
            else
            {
                SetSettingsHint(string.Empty);
                SendChangeRequest(result, _roomInfo.ThinkingTime, _roomInfo.MaxPlayerCount);
            }

        }

        private void ChangeTime(string value)
        {
            if (!System.Single.TryParse(value, out var result) || result <= 0 || float.IsNaN(result) || float.IsInfinity(result))
            {
                _thinkingTime.SetTextWithoutNotify(_roomInfo.ThinkingTime.ToString());
                SetSettingsHint("思考时间须为大于 0 的有效秒数，已恢复原值。");
                return;
            }
            else
            {
                SetSettingsHint(string.Empty);
                SendChangeRequest(_roomInfo.SmallBlindChips, result, _roomInfo.MaxPlayerCount);
            }
        }

        private void ChangeInterHandWait(string value)
        {
            if (!int.TryParse(value, out var seconds) || seconds < RoomInfo.MinInterHandWaitSeconds ||
                seconds > RoomInfo.MaxInterHandWaitSeconds)
            {
                _interHandWait.SetTextWithoutNotify(_roomInfo.InterHandWaitSeconds.ToString());
                SetSettingsHint("下一手等待时间须为 1～60 秒，已恢复原值。");
                return;
            }
            SetSettingsHint(string.Empty);
            SendChangeRequest(_roomInfo.SmallBlindChips, _roomInfo.ThinkingTime, _roomInfo.MaxPlayerCount, seconds);
        }

        private void ChangeMaxPlayer(string value)
        {
            if (!System.Int32.TryParse(value, out var result) || result < 2 || result > 16 || result < _roomInfo.PlayerCount)
            {
                _maxPlayer.SetTextWithoutNotify(_roomInfo.MaxPlayerCount.ToString());
                SetSettingsHint($"参战人数须为 {Math.Max(2, _roomInfo.PlayerCount)} 至 16 的整数，已恢复原值。");
                return;
            }
            else
            {
                SetSettingsHint(string.Empty);
                SendChangeRequest(_roomInfo.SmallBlindChips, _roomInfo.ThinkingTime, result);
            }
        }

        private void SetSettingsHint(string message)
        {
            if (_settingsHint != null)
            {
                _settingsHint.text = message;
                _settingsHint.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
        }

        private void Refresh()
        {
            bool interactable = IsOwner && _roomInfo.IsWaiting;
            _smallBlindChips.interactable = interactable;
            _thinkingTime.interactable = interactable;
            _interHandWait.interactable = interactable;
            _maxPlayer.interactable = interactable;

            _title.text = $"房间{_roomInfo.Id}";
            _smallBlindChips.text = _roomInfo.SmallBlindChips.ToString();
            _thinkingTime.text = _roomInfo.ThinkingTime.ToString();
            _interHandWait.SetTextWithoutNotify(_roomInfo.InterHandWaitSeconds.ToString());
            _maxPlayer.text = _roomInfo.MaxPlayerCount.ToString();

            if (_accountStatus != null && GameController.Available)
            {
                var state = GameController.Instance.State;
                _accountStatus.text = $"{state.UserName}  ·  ${state.Chips:N0}";
            }
            if (_roomStatus != null)
            {
                var bigBlind = checked((uint)_roomInfo.SmallBlindChips * 2U);
                _roomStatus.text = IsSpectator
                    ? GameController.Instance.State.Chips < bigBlind
                        ? $"旁观中 · 至少需要 ${bigBlind:N0} 才能入局"
                        : "旁观中 · 等待空座"
                    : _roomInfo.IsWaiting ? "等待房主开始" : "牌局进行中";
            }

            if (IsOwner)
            {
                _ready.gameObject.SetActive(false);
                _cancelReady.gameObject.SetActive(false);
                _start.gameObject.SetActive(_roomInfo.IsWaiting);
                _start.interactable = _roomInfo.PlayerCount >= 2;
                for (int i = 0; i < _roomInfo.PlayerCount; i++)
                {
                    if (_roomInfo.Players[i] != SelfName && !_roomInfo.ReadyStatus[i])
                        _start.interactable = false;
                }
            }
            else if (IsSpectator)
            {
                _ready.gameObject.SetActive(false);
                _cancelReady.gameObject.SetActive(false);
                _start.gameObject.SetActive(false);
            }
            else
            {
                _ready.gameObject.SetActive(!IsReady);
                _cancelReady.gameObject.SetActive(IsReady);
                _start.gameObject.SetActive(false);
            }

            foreach (var tmp in _tmpPlayerItems)
                Destroy(tmp.gameObject);
            _tmpPlayerItems.Clear();
            for(int i = 0; i < RoomInfo.PlayerCount; i++)
            {
                var playerName = RoomInfo.Players[i];
                GameObject obj = Instantiate(_player, parent:_playerList);
                var component = obj.GetComponent<PlayerListItem>();
                component.PlayerName = playerName;
                component.SetNamecard(RoomInfo.GetNamecard(playerName), RoomInfo.GetServiceYears(playerName));
                component.IsOwner = _roomInfo.OwnerName == playerName;
                component.IsReady = component.IsOwner ? false : RoomInfo.ReadyStatus[i];
                _tmpPlayerItems.Add(component);
            }
            foreach (var spectatorName in RoomInfo.Spectators)
            {
                GameObject obj = Instantiate(_player, parent: _playerList);
                var component = obj.GetComponent<PlayerListItem>();
                component.PlayerName = spectatorName + "（旁观）";
                component.IsOwner = _roomInfo.OwnerName == spectatorName;
                component.IsReady = false;
                component.SetNamecard(RoomInfo.GetNamecard(spectatorName), RoomInfo.GetServiceYears(spectatorName));
                _tmpPlayerItems.Add(component);
            }
        }

        private void SendChangeRequest(int smallBlindChips, float thinkingTime, int maxPlayer, int? interHandWait = null)
        {
            if (!NetworkClient.isConnected || NetworkClient.connection == null ||
                !NetworkClient.connection.isAuthenticated || !GameController.Available ||
                !GameController.Instance.State.IsAuthenticated)
                return;
            if (!IsOwner || !_roomInfo.IsWaiting)
                return;
            NetworkClient.Send(new RoomInfoChange()
            {
                RoomId = _roomInfo.Id,
                PlayerName = SelfName,
                InitialChips = _roomInfo.InitialChips,
                SmallBlindChips = smallBlindChips,
                ThinkingTime = thinkingTime,
                InterHandWaitSeconds = interHandWait ?? _roomInfo.InterHandWaitSeconds,
                MaxPlayer = maxPlayer,
            });
        }
    }
}
