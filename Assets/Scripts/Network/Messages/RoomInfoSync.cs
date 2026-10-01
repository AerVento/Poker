using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    public enum RoomTransitionReason : byte
    {
        None,
        InsufficientPlayers,
        GameStopped,
    }

    /// <summary>
    /// 由服务器发给客户端，同步各个客户端之间的信息
    /// </summary>
    public struct RoomInfoSync : NetworkMessage
    {
        public RoomTransitionReason TransitionReason;
        public RoomInfo RoomInfo;
    }
}
