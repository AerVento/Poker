using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct GameResultNotification : NetworkMessage
    {
        public string RawData;
        public GameResult GameResult
        {
            get => JsonSerializer.Deserialize<GameResult>(RawData);
            set => RawData = JsonSerializer.Serialize(value);
        }
    }
}