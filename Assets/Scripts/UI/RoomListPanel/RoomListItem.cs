using Game.Network;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.RoomListPanelElement
{
    public class RoomListItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _id;
        
        [SerializeField]
        private TextMeshProUGUI _owner;
        
        [SerializeField]
        private TextMeshProUGUI _capacity;

        [SerializeField]
        private Button _join;

        public Button.ButtonClickedEvent OnClick => _join.onClick;

        private void OnDestroy()
        {
            _join.onClick.RemoveAllListeners();
        }

        public void SetRoomInfo(RoomInfo info)
        {
            _id.text = info.Id.ToString();
            _owner.text = info.OwnerName + (info.IsWaiting ? " · 等待" : " · 牌局中");
            _capacity.text = $"参战 {info.PlayerCount}/{info.MaxPlayerCount} · 旁观 {info.Spectators.Count}";
        }

    }
}

