using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Network.Messages
{
    public struct PlayerLeaveRoomRequest : NetworkMessage
    {
        public string PlayerName;
        public int RoomId;
    }
}
