using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    public enum PlayerOperationType
    {
        Check,
        Bet,
        Raise,
        Call,
        AllIn,
        Fold
    }
    [System.Serializable]
    public class PlayerOperation
    {
        public PlayerOperationType Type { get; set; }
        /// <summary>
        /// 当Type为Bet、Raise、Call、In时，此字段有效，标明加注的数量。
        /// </summary>
        public uint Number { get; set; }
    }
}
