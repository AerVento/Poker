using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    public struct PlayerChangeNameMessage : NetworkMessage
    {
        public string OldName;
        public string NewName;
    }
}
