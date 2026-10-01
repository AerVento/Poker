using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Network
{
    public struct RoomListResponse : NetworkMessage
    {
        public RoomList RoomList;
    }
}
