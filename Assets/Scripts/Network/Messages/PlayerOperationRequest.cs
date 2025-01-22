using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct PlayerOperationRequest : NetworkMessage
    {
        public int RoomId;
        public int PlayerIndex;
        public PlayerOperationType Type;
        /// <summary>
        /// 当Type为Bet时，此字段有效，标明加注的数量。
        /// </summary>
        public uint Number;
    }
}
