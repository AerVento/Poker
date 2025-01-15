using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct PlayerConnectMessage : NetworkMessage
    {
        /// <summary>
        /// 玩家名称, 是玩家之间的唯一标识
        /// </summary>
        public string PlayerName;
    }
}
