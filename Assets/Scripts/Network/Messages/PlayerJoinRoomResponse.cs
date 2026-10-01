using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct PlayerJoinRoomResponse : NetworkMessage
    {
        /// <summary>
        /// 0 = success, -1 = room doesn't exist, -2 = already associated with a room,
        /// -3 = full room, -5 = account storage unavailable, -6 = fewer than one big blind
        /// </summary>
        public int SuccessCode;
        public RoomInfo RoomInfo;
    }
}
