using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Framework.UI;
using UnityEngine.UI;
using TMPro;
using Game.Network;
namespace Game.UI
{
    public class RoomListPanel : SingletonPanel<RoomListPanel>
    {
        [SerializeField]
        private Button _back;
        [SerializeField]
        private Transform _list;
        [SerializeField]
        private GameObject _roomItem;
        [SerializeField]
        private ScrollRect _scroll;

        private List<RoomInfo> _rooms = new List<RoomInfo>() {
            //new RoomInfo("AerVento", 8),
            //new RoomInfo("Schoparks", 4),
            //new RoomInfo("yurzhang", 2),
            //new RoomInfo("kakie", 4)
        };

        private List<GameObject> _tmpRoomItem = new List<GameObject>();

        private void OnEnable()
        {
            _back.onClick.AddListener(Back);
        }

        private void OnDisable()
        {
            _back.onClick.RemoveListener(Back);
        }

        private void Start()
        {
            foreach (var tmp in _tmpRoomItem)
                Destroy(tmp);
            _tmpRoomItem.Clear();
            foreach(var room in _rooms)
            {
                GameObject obj = Instantiate(_roomItem, _list);
                var component = obj.GetComponent<RoomListItem>();
                component.SetRoomInfo(room);
                _tmpRoomItem.Add(obj);
            }
            var size = _scroll.content.sizeDelta;
            size.y = 160 * _tmpRoomItem.Count + 120;
            _scroll.content.sizeDelta = size;
        }

        private void Back()
        {
            UIManager.Instance.HidePanel<RoomListPanel>();
            UIManager.Instance.ShowPanel<TitlePanel>();
        }
    }
}
