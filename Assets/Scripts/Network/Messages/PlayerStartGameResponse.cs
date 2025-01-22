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
        /// 0 = success, -1 = room doesn't exist, -2 = player not readied, -3 = owner not in the room, -4 = not owner, -5 = too few player ( < 2)
        /// </summary>
        public int SuccessCode;
    }
}
