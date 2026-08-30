using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct PlayerStartGameResponse : NetworkMessage
    {
        /// <summary>
        /// 0 = success, -1 = room doesn't exist, -2 = player not readied,
        /// -3 = owner not in the room, -4 = not owner, -5 = too few players,
        /// -6 = game already started, -7 = too few eligible players,
        /// -8 = account or hand storage unavailable, -9 = table balance limit exceeded
        /// </summary>
        public int SuccessCode;
    }
}
