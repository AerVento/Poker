using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Framework.UI;
using UnityEngine.UI;
using TMPro;
using Game.Network;
using Mirror;
using Cysharp.Threading.Tasks;
using RoomListItem = Game.UI.RoomListPanelElement.RoomListItem;

namespace Game.UI
{
    public class RoomListPanel : SingletonPanel<RoomListPanel>
    {
        [SerializeField]
        private Button _back;

        [SerializeField]
        private Button _refresh;

        [SerializeField]
        private Button _createRoom;

        [SerializeField]
        private Button _settings;

        [SerializeField]
        private ScrollRect _scroll;

        [SerializeField]
        private Transform _list;

        [SerializeField]
        private GameObject _roomItem;

        [SerializeField]
        private TextMeshProUGUI _accountStatus;

        /// <summary>
        /// 持续刷新
        /// </summary>
        private bool _refreshRoutineFlag = false;
        private UniTask? _refreshRoutine = null;

        private RoomList _roomList = new RoomList();
        private List<RoomListItem> _tmpRoomItem = new List<RoomListItem>();

        /// <summary>
        /// 显示的房间列表
        /// </summary>
        public RoomList Rooms
        {
            get => _roomList;
            set
            {
                _roomList = value;

                // 移除老的
                foreach (var tmp in _tmpRoomItem)
                {
                    tmp.OnClick.RemoveAllListeners();
                    Destroy(tmp.gameObject);
                }
                _tmpRoomItem.Clear();

                // 添加新的
                foreach (var room in _roomList.Rooms)
                {
                    GameObject obj = Instantiate(_roomItem, _list);
                    var component = obj.GetComponent<RoomListItem>();
                    component.SetRoomInfo(room);
                    component.OnClick.AddListener(() =>
                    {
                        JoinRoom(room.Id);
                    });

                    _tmpRoomItem.Add(component);
                }

            }
        }


        private void OnEnable()
        {
            RefreshAccountStatus();
            NetworkClient.Send(new RoomListRequest());
            _back.onClick.AddListener(Back);
            _refresh.onClick.AddListener(RefreshRoomList);
            _createRoom.onClick.AddListener(CreateRoom);
            _settings.onClick.AddListener(Settings);

            _refreshRoutineFlag = true;
            if (_refreshRoutine == null)
            {
                _refreshRoutine = UniTask.Create(async () =>
                {
                    do
                    {
                        RefreshRoomList();
                        await UniTask.WaitForSeconds(1);
                    } while (_refreshRoutineFlag);
                    _refreshRoutine = null;
                });
            }
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _refresh.onClick.RemoveListener(RefreshRoomList);
            _createRoom.onClick.RemoveListener(CreateRoom);
            _settings.onClick.RemoveListener(Settings);
            _refreshRoutineFlag = false;
        }

        private void Start()
        {

        }

        private void Back()
        {
            UIManager.Instance.HidePanel<RoomListPanel>();
            UIManager.Instance.ShowPanel<TitlePanel>();
        }

        private void Settings()
        {
            UIManager.Instance.HidePanel<RoomListPanel>();
            UIManager.Instance.ShowPanel<OptionPanel>();
        }

        public void RefreshRoomList()
        {
            NetworkClient.Send(new RoomListRequest());
        }

        public void RefreshAccountStatus()
        {
            if (_accountStatus == null || !GameController.Available)
                return;

            var state = GameController.Instance.State;
            _accountStatus.text = $"{state.UserName}  ·  ${state.Chips:N0}";
        }

        private void CreateRoom()
        {
            NetworkClient.Send(new PlayerCreateRoomRequest() { PlayerName = GameController.Instance.State.Nickname });
            UIManager.Instance.HidePanel<RoomListPanel>();
            UIManager.Instance.ShowPanel<RoomPanel>();
        }

        private void JoinRoom(int roomId)
        {
            NetworkClient.Send(new PlayerJoinRoomRequest() { 
                PlayerName = GameController.Instance.State.Nickname,
                RoomId = roomId
            });
        }
    }
}
