using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Network
{
    /// <summary>
    /// 房间信息
    /// </summary>
    [System.Serializable]
    public class RoomInfo
    {
        /// <summary>
        /// 房间号码
        /// </summary>
        public int Id { get; private set; }
        
        /// <summary>
        /// 当前玩家数量
        /// </summary>
        public int PlayerCount => PlayerNames.Count;

        /// <summary>
        /// 最大玩家数量
        /// </summary>
        public int MaxPlayerCount { get; private set; }

        /// <summary>
        /// 房主名
        /// </summary>
        public string OwnerName { get; private set; }

        /// <summary>
        /// 玩家名
        /// </summary>
        public List<string> PlayerNames { get; private set; } = new List<string>();

        public RoomInfo(string owner, int maxPlayer) 
        {
            OwnerName = owner;
            MaxPlayerCount = maxPlayer;
            PlayerNames.Add(owner);
        }
    }
}

