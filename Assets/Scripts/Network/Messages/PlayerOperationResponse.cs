using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct PlayerOperationResponse : NetworkMessage
    {
        public int SuccessCode;
        public string HandId;
        public ulong TurnId;
        public int RoomId;
        public PlayerOperationType Type;
    }
}
