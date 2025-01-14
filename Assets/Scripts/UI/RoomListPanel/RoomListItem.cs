using Game.Network;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace Game.UI
{
    public class RoomListItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _id;
        
        [SerializeField]
        private TextMeshProUGUI _owner;
        
        [SerializeField]
        private TextMeshProUGUI _capacity;

        public void SetRoomInfo(RoomInfo info)
        {
            _id.text = info.Id.ToString();
            _owner.text = info.OwnerName;
            _capacity.text = $"{info.PlayerCount}/{info.MaxPlayerCount}";
        }
    }
}

