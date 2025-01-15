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
    /// 由服务器发给客户端，同步各个客户端之间的信息
    /// </summary>
    public struct RoomInfoSync : NetworkMessage
    {
        public string RawData;
        public RoomInfo RoomInfo
        {
            get => JsonSerializer.Deserialize<RoomInfo>(RawData);
            set => RawData = JsonSerializer.Serialize(value);
        }
    }
}
