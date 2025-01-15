using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Game.Network
{
    public struct RoomListResponse : NetworkMessage
    {
        public string RawData;
        public RoomList RoomList
        {
            get => JsonSerializer.Deserialize<RoomList>(RawData);
            set => RawData = JsonSerializer.Serialize(value);
        }

    }
}
