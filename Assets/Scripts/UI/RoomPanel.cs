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
        private TMP_InputField _initialChips;

        [SerializeField]
        private TMP_InputField _thinkingTime;

        [SerializeField]
        private TMP_InputField _maxPlayer;

        [SerializeField]
        private Transform _playerList;

        /// <summary>
        /// UIԤ����
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
        /// ��ǰ����Ƿ��Ƿ���
        /// </summary>
        public bool IsOwner => SelfName == RoomInfo.OwnerName;

        /// <summary>
        /// ��ǰ����Ƿ���׼��
        /// </summary>
        public bool IsReady
        {
            get
            {
                // ����Ƿ�������Ĭ�Ϸ����Ѿ�׼��
                if (IsOwner)
                    return true;

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
            _back.onClick.AddListener(Back);
            _ready.onClick.AddListener(ChangeReady);
            _cancelReady.onClick.AddListener(ChangeReady);
            _start.onClick.AddListener(StartGame);
            _initialChips.onEndEdit.AddListener(ChangeChip);
            _thinkingTime.onEndEdit.AddListener(ChangeTime);
            _maxPlayer.onEndEdit.AddListener(ChangeMaxPlayer);
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _ready.onClick.RemoveListener(ChangeReady);
            _cancelReady.onClick.RemoveListener(ChangeReady);
            _start.onClick.RemoveListener(StartGame);
            _initialChips.onEndEdit.RemoveListener(ChangeChip);
            _thinkingTime.onEndEdit.RemoveListener(ChangeTime);
            _maxPlayer.onEndEdit.RemoveListener(ChangeMaxPlayer);

        }



        private void Back()
        {
            NetworkClient.Send(new PlayerLeaveRoomRequest()
            {
                PlayerName = GameController.Instance.State.Nickname,
                RoomId = _roomInfo.Id
            });
            UIManager.Instance.HidePanel<RoomPanel>();
            UIManager.Instance.ShowPanel<RoomListPanel>();
        }

        private void ChangeReady()
        {
            NetworkClient.Send(new PlayerChangeReadyMessage() { PlayerName = SelfName, RoomId = _roomInfo.Id });
        }

        private void StartGame()
        {
            NetworkClient.Send(new PlayerStartGameRequest() { PlayerName = SelfName, RoomId = _roomInfo.Id });
        }

        private void ChangeChip(string value)
        {
            if (!System.Int32.TryParse(value, out var result) || result <= 0)
            {
                _initialChips.text = _roomInfo.InitialChips.ToString();
                return;
            }
            // ��ֹ����������ʱ���
            else if(result >= 10000000)
            {
                _initialChips.text = "10000000";
                _roomInfo.InitialChips = 10000000;
                return;
            }
            else
            {
                SendChangeRequest(result, _roomInfo.ThinkingTime, _roomInfo.MaxPlayerCount);
            }

        }

        private void ChangeTime(string value)
        {
            if (!System.Single.TryParse(value, out var result) || result < 0)
            {
                _initialChips.text = _roomInfo.ThinkingTime.ToString();
                return;
            }
            else
            {
                SendChangeRequest(_roomInfo.InitialChips, result, _roomInfo.MaxPlayerCount);
            }
        }

        private void ChangeMaxPlayer(string value)
        {
            if (!System.Int32.TryParse(value, out var result) || result < 2 || result > 16)
            {
                _maxPlayer.text = _roomInfo.InitialChips.ToString();
                return;
            }
            else
            {
                SendChangeRequest(_roomInfo.InitialChips, _roomInfo.ThinkingTime, result);
            }
        }

        private void Refresh()
        {
            bool interactable = IsOwner;
            _initialChips.interactable = interactable;
            _thinkingTime.interactable = interactable;
            _maxPlayer.interactable = interactable;

            _title.text = $"����{_roomInfo.Id}";
            _initialChips.text = _roomInfo.InitialChips.ToString();
            _thinkingTime.text = _roomInfo.ThinkingTime.ToString();
            _maxPlayer.text = _roomInfo.MaxPlayerCount.ToString();

            if (IsOwner)
            {
                _ready.gameObject.SetActive(false);
                _cancelReady.gameObject.SetActive(false);
                _start.gameObject.SetActive(true);
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
                component.IsOwner = _roomInfo.OwnerName == playerName;
                component.IsReady = component.IsOwner ? false : RoomInfo.ReadyStatus[i];
                _tmpPlayerItems.Add(component);
            }
        }

        private void SendChangeRequest(int initialChips, float thinkingTime, int maxPlayer)
        {
            NetworkClient.Send(new RoomInfoChange()
            {
                RoomId = _roomInfo.Id,
                PlayerName = SelfName,
                InitialChips = initialChips,
                ThinkingTime = thinkingTime,
                MaxPlayer = maxPlayer,
            });
        }
    }
}