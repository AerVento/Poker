using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    /// <summary>
    /// 建立连接和玩家名之间的映射关系
    /// </summary>
    public class PlayerManager
    {
        private Dictionary<string, NetworkConnectionToClient> _conns = new Dictionary<string, NetworkConnectionToClient>();
        public bool IsConnected(string playerName)
        {
            return _conns.ContainsKey(playerName);
        }
        
        public NetworkConnectionToClient GetConnection(string playerName)
        {
            if (_conns.ContainsKey(playerName))
                return _conns[playerName];
            else
                return null;
        }

        public void SetConnection(string playerName, NetworkConnectionToClient conn)
        {
            if(_conns.ContainsKey(playerName))
                _conns[playerName] = conn;
            else
                _conns.Add(playerName, conn);
        }

        public void RemoveConnection(string playerName)
        {
            _conns.Remove(playerName);   
        }

        /// <summary>
        /// 移除连接
        /// </summary>
        /// <param name="conn">连接</param>
        /// <param name="playerName">此连接对应的用户名</param>
        /// <returns>是否存在此链接</returns>
        public bool RemoveConnection(NetworkConnectionToClient conn, out string playerName)
        {
            playerName = null;
            foreach(var pair in _conns)
            {
                if(pair.Value == conn)
                {
                    playerName = pair.Key;
                    _conns.Remove(pair.Key);
                    return true;
                }
            }
            return false;
        }


    }
}
