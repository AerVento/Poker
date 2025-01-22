using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Game.Network
{
    /// <summary>
    /// 由客户端发给服务器，修改房间信息
    /// </summary>
    public struct RoomInfoChange : NetworkMessage
    {
        /// <summary>
        /// 房间Id
        /// </summary>
        public int RoomId;

        /// <summary>
        /// 是哪个玩家做了修改？
        /// </summary>
        public string PlayerName;

        /// <summary>
        /// 初始筹码
        /// </summary>
        public int InitialChips;

        /// <summary>
        /// 小盲注
        /// </summary>
        public int SmallBlindChips;

        /// <summary>
        /// 思考时间（秒）
        /// </summary>
        public float ThinkingTime;

        /// <summary>
        /// 最大玩家数（2~16）
        /// </summary>
        public int MaxPlayer;
    }
}
