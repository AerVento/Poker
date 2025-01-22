using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    /// <summary>
    /// 一轮游戏结束后的结果
    /// </summary>
    [System.Serializable]
    public class RoundResult
    {
        public List<Poker> Community { get; set; } = new List<Poker>();
        public List<Player> PlayerResults { get; set; } = new List<Player>();
        [System.Serializable]
        public class Player
        {
            public string PlayerName { get; set; }
            public bool IsFold { get; set; }
            public bool IsDisconnected { get; set; }
            public List<Poker> Hole { get; set; } = new List<Poker>();
            public uint ChipEarned { get; set; }
        }
    }
}
