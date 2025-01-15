using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Framework.UI;
using UnityEngine.UI;
using TMPro;
using Game.Network;
using Mirror;
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
        private ScrollRect _scroll;

        [SerializeField]
        private Transform _list;

        [SerializeField]
        private GameObject _roomItem;


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
                    // 只显示正在等待开始的房间
                    if (!room.IsWaiting)
                        continue;

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
            NetworkClient.Send(new RoomListRequest());
            _back.onClick.AddListener(Back);
            _refresh.onClick.AddListener(RefreshRoomList);
            _createRoom.onClick.AddListener(CreateRoom);
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _refresh.onClick.RemoveListener(RefreshRoomList);
            _createRoom.onClick.RemoveListener(CreateRoom);
        }

        private void Start()
        {

        }

        private void Back()
        {
            UIManager.Instance.HidePanel<RoomListPanel>();
            UIManager.Instance.ShowPanel<TitlePanel>();
        }

        public void RefreshRoomList()
        {
            NetworkClient.Send(new RoomListRequest());
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
