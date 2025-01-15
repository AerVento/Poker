using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    [System.Serializable]
    public class RoomList
    {
        public List<RoomInfo> Rooms { get; set; } = new List<RoomInfo>();
    }
}
