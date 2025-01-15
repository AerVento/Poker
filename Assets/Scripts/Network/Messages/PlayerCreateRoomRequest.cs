using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct PlayerCreateRoomRequest : NetworkMessage
    {
        public string PlayerName;
    }
}