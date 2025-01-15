using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct PlayerJoinRoomResponse : NetworkMessage
    {
        /// <summary>
        /// 0 = success, -1 = room doesn't exist, -2 = already in the room, -3 = full room
        /// </summary>
        public int SuccessCode;
        public string RawData;
        public RoomInfo RoomInfo
        {
            get => JsonSerializer.Deserialize<RoomInfo>(RawData);
            set => RawData = JsonSerializer.Serialize(value);
        }
    }
}
