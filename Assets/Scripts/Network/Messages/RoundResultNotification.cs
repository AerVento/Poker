using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct RoundResultNotification : NetworkMessage
    {
        public string RawData;
        public RoundResult RoundResult
        {
            get => JsonSerializer.Deserialize<RoundResult>(RawData);
            set => RawData = JsonSerializer.Serialize(value);
        }
    }
}
