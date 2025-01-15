using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;
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
        public int Id { get; set; }

        /// <summary>
        /// 最大玩家数量
        /// </summary>
        public int MaxPlayerCount { get; set; } = 8;

        /// <summary>
        /// 房主名
        /// </summary>
        public string OwnerName { get; set; }

        /// <summary>
        /// 玩家名
        /// </summary>
        public List<string> Players { get; set; } = new List<string>();

        /// <summary>
        /// 玩家的准备状态
        /// </summary>
        public List<bool> ReadyStatus { get; set; } = new List<bool>();

        /// <summary>
        /// 初始筹码
        /// </summary>
        public int InitialChips { get; set; } = 300;

        /// <summary>
        /// 思考时间（秒）
        /// </summary>
        public float ThinkingTime { get; set; } = 30;

        /// <summary>
        /// 玩家们是否在等待开始
        /// </summary>
        public bool IsWaiting { get; set; } = true;

        /// <summary>
        /// 当前玩家数量
        /// </summary>
        [JsonIgnore]
        public int PlayerCount => Players.Count;

        public static RoomInfo CreateRoom(string owner) 
        {
            RoomInfo room = new RoomInfo();
            room.OwnerName = owner;
            room.Players.Add(owner);
            return room;
        }
    }
}

