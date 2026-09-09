using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Framework.UI;
using UnityEngine.UI;
using TMPro;
using Game.Network;
using Mirror;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Linq;
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
        private CancellationTokenSource _refreshCancellation;

        private RoomList _roomList = new RoomList();
        private readonly Dictionary<int, RoomListItem> _roomItems = new Dictionary<int, RoomListItem>();

        /// <summary>
        /// 显示的房间列表
        /// </summary>
        public RoomList Rooms
        {
            get => _roomList;
            set
            {
                _roomList = value ?? new RoomList();
                var rooms = _roomList.Rooms ?? new List<RoomInfo>();
                var roomIds = new HashSet<int>(rooms.Select(room => room.Id));
                foreach (var roomId in _roomItems.Keys.ToList())
                {
                    if (roomIds.Contains(roomId))
                        continue;
                    var item = _roomItems[roomId];
                    item.gameObject.SetActive(false);
                    Destroy(item.gameObject);
                    _roomItems.Remove(roomId);
                }

                // Keep existing buttons alive during periodic refreshes so clicks and focus survive.
                for (int i = 0; i < rooms.Count; i++)
                {
                    var room = rooms[i];
                    if (!_roomItems.TryGetValue(room.Id, out var item))
                    {
                        item = Instantiate(_roomItem, _list).GetComponent<RoomListItem>();
                        int roomId = room.Id;
                        item.OnClick.AddListener(() => JoinRoom(roomId));
                        _roomItems.Add(roomId, item);
                    }
                    item.SetRoomInfo(room);
                    item.transform.SetSiblingIndex(i);
                }

            }
        }


        private void OnEnable()
        {
            GameAudioManager.EnsureClient()?.EnterLobby();
            RefreshAccountStatus();
            RefreshRoomList();
            _back.onClick.AddListener(Back);
            _refresh.onClick.AddListener(RefreshRoomList);
            _createRoom.onClick.AddListener(CreateRoom);
            _settings.onClick.AddListener(Settings);

            _refreshCancellation = new CancellationTokenSource();
            RefreshLoop(_refreshCancellation.Token).Forget();
        }

        private async UniTask RefreshLoop(CancellationToken cancellationToken)
        {
            while (!await UniTask.Delay(1000, cancellationToken: cancellationToken).SuppressCancellationThrow())
                RefreshRoomList();
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
            _refresh.onClick.RemoveListener(RefreshRoomList);
            _createRoom.onClick.RemoveListener(CreateRoom);
            _settings.onClick.RemoveListener(Settings);
            _refreshCancellation?.Cancel();
            _refreshCancellation?.Dispose();
            _refreshCancellation = null;
        }

        private void Update()
        {
            bool pending = NetworkManager.singleton is NetworkRoom room && room.IsRoomEntryPending;
            _back.interactable = !pending;
            _settings.interactable = !pending;
            _createRoom.interactable = !pending;
        }

        private void Back()
        {
            if (NetworkManager.singleton is NetworkRoom room && room.IsRoomEntryPending)
                return;
            UIManager.Instance.PopPanel<RoomListPanel>();
        }

        private void Settings()
        {
            if (NetworkManager.singleton is NetworkRoom room && room.IsRoomEntryPending)
                return;
            UIManager.Instance.PushPanel<OptionPanel>();
        }

        public void RefreshRoomList()
        {
            if (isActiveAndEnabled && NetworkClient.isConnected &&
                NetworkClient.connection != null && NetworkClient.connection.isAuthenticated)
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
            (NetworkManager.singleton as NetworkRoom)?.RequestCreateRoom();
        }

        private void JoinRoom(int roomId)
        {
            (NetworkManager.singleton as NetworkRoom)?.RequestJoinRoom(roomId);
        }
    }
}
