using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    [System.Serializable]
    public class GameResult
    {
        public List<Player> PlayerResults { get; set; } = new List<Player>();
        [System.Serializable]
        public class Player
        {
            public string Name { get; set; }
            public uint Chips { get; set; }
            public bool IsDisconnected { get; set; }
        }
    }
}
