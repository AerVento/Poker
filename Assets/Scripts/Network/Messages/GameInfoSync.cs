using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct GameInfoSync : NetworkMessage
    {
        public string RawData;
        public ClientGameInfo Info
        {
            get => JsonSerializer.Deserialize<ClientGameInfo>(RawData);
            set => RawData = JsonSerializer.Serialize(value);
        }
    }
}
