using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Framework.UI;
using UnityEngine.UI;
using TMPro;
using Game.Network;
using Mirror;
using Assets.Scripts.Network.Messages;
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



        private void OnEnable()
        {
            _back.onClick.AddListener(Back);
            _ready.onClick.AddListener(Ready);
            _initialChips.onEndEdit.AddListener(ChangeChip);
            _thinkingTime.onEndEdit.AddListener(ChangeTime);
            _maxPlayer.onEndEdit.AddListener(ChangeMaxPlayer);

            Refresh();
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _ready.onClick.RemoveListener(Ready);
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

        private void Ready()
        {

        }

        private void ChangeChip(string value)
        {
            if (!System.Int32.TryParse(value, out var result) || result <= 0)
            {
                _initialChips.text = _roomInfo.InitialChips.ToString();
                return;
            }
            // 防止筹码量过大时溢出
            else if(result >= 10000000)
            {
                _initialChips.text = "10000000";
                _roomInfo.InitialChips = 10000000;
                return;
            }
            else
            {
                _roomInfo.InitialChips = result;
                SendChangeRequest();
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
                _roomInfo.ThinkingTime = result;
                SendChangeRequest();
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
                _roomInfo.MaxPlayerCount = result;
                SendChangeRequest();
            }
        }

        private void Refresh()
        {
            bool settingInteractable = _roomInfo.OwnerName == GameController.Instance.State.Nickname;
            _initialChips.interactable = settingInteractable;
            _thinkingTime.interactable = settingInteractable;
            _maxPlayer.interactable = settingInteractable;

            _title.text = $"房间{_roomInfo.Id}";
            _initialChips.text = _roomInfo.InitialChips.ToString();
            _thinkingTime.text = _roomInfo.ThinkingTime.ToString();
            _maxPlayer.text = _roomInfo.MaxPlayerCount.ToString();

            foreach (var tmp in _tmpPlayerItems)
                Destroy(tmp.gameObject);
            _tmpPlayerItems.Clear();
            foreach(var playerName in _roomInfo.Players)
            {
                GameObject obj = Instantiate(_player, parent:_playerList);
                var component = obj.GetComponent<PlayerListItem>();
                component.PlayerName = playerName;
                component.IsOwner = _roomInfo.OwnerName == playerName;
                _tmpPlayerItems.Add(component);
            }
        }

        private void SendChangeRequest()
        {
            NetworkClient.Send(new RoomInfoChange()
            {
                RoomId = _roomInfo.Id,
                PlayerName = GameController.Instance.State.Nickname,
                InitialChips = _roomInfo.InitialChips,
                ThinkingTime = _roomInfo.ThinkingTime,
                MaxPlayer = _roomInfo.MaxPlayerCount,
            });
        }
    }
}