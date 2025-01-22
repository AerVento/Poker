using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Game.Network.ServerGameInfo;

namespace Game.Network
{
    /// <summary>
    /// 客户端管理的游戏信息
    /// 客户端利用此信息来显示游戏界面
    /// </summary>
    public struct ClientGameInfo
    {
        public RoomInfo RoomInfo { get; set; }

        public List<PlayerInfo> Players { get; set; }

        [JsonIgnore]
        public int PlayersCount => RoomInfo.Players.Count;

        public GameStage Round { get; set; }

        /// <summary>
        /// 总奖池金额
        /// </summary>
        public uint TotalPot { get; set; }

        /// <summary>
        /// 庄家位置
        /// </summary>
        public int Dealer { get; set; }

        /// <summary>
        /// 小盲位置
        /// </summary>
        public int SmallBlind { get; set; }

        /// <summary>
        /// 大盲位置
        /// </summary>
        public int BigBlind { get; set; }

        /// <summary>
        /// 正在操作的玩家
        /// </summary>
        public int OperatingPlayer { get; set; }

        /// <summary>
        /// 公共牌
        /// </summary>
        public List<Poker> Community { get; set; }

        /// <summary>
        /// 上轮中最高加注的筹码量
        /// </summary>
        public uint LastHighestRaise { get; set; }
        
        /// <summary>
        /// 此轮中最高加注的筹码量
        /// </summary>
        public uint CurrentHighestRaise { get; set; }

        /// <summary>
        /// 加注时最小加注量
        /// </summary>
        [JsonIgnore]
        public uint LeastRaise => System.Math.Max(LastHighestRaise, CurrentHighestRaise);
        
        /// <summary>
        /// 此轮中下注最高的玩家下注的数量
        /// </summary>
        public uint CurrentHighestPlayerBet { get; set; }

        /// <summary>
        /// 此玩家的下标
        /// </summary>
        public int CurrentPlayerIndex { get; set; }

        /// <summary>
        /// 当前玩家信息
        /// </summary>
        [JsonIgnore]
        public PlayerInfo CurrentPlayer => Players[CurrentPlayerIndex];

        /// <summary>
        /// 此玩家底牌
        /// </summary>
        public List<Poker> Hole { get; set; }

        /// <summary>
        /// 为特定玩家创造一个客户端游戏信息
        /// </summary>
        /// <param name="serverGameInfo"></param>
        /// <param name="playerIndex"></param>
        /// <returns></returns>
        public ClientGameInfo (ServerGameInfo serverGameInfo, int playerIndex)
        {
            RoomInfo = serverGameInfo.RoomInfo;
            Players = new List<PlayerInfo>();
            for(int i = 0; i < serverGameInfo.PlayersCount; i++)
            {
                Players.Add(new PlayerInfo()
                {
                    Chips = serverGameInfo.Players[i].Chips,
                    PlayerName = serverGameInfo.Players[i].PlayerName,
                    StageBet = serverGameInfo.Players[i].StageBet,
                    RoundBet = serverGameInfo.Players[i].RoundBet,
                    LastOperation = serverGameInfo.Players[i].LastOperation,
                    IsFold = serverGameInfo.Players[i].IsFold,
                    IsAllIn = serverGameInfo.Players[i].IsAllIn,
                    IsDisconnected = serverGameInfo.Players[i].IsDisconnected,
                });
            }

            Round = serverGameInfo.Stage;
            TotalPot = serverGameInfo.TotalPot;
            Dealer = serverGameInfo.Dealer;
            SmallBlind = serverGameInfo.SmallBlind;
            BigBlind = serverGameInfo.BigBlind;
            OperatingPlayer = serverGameInfo.OperatingPlayer;
            Community = serverGameInfo.Community;
            LastHighestRaise = serverGameInfo.LastHighestRaise;
            CurrentHighestRaise = serverGameInfo.CurrentHighestRaise;
            CurrentHighestPlayerBet = serverGameInfo.CurrentHighestPlayerBet;

            CurrentPlayerIndex = playerIndex;
            Hole = serverGameInfo.Players[playerIndex].Hole;
        }


        [System.Serializable]
        public struct PlayerInfo
        {
            /// <summary>
            /// 筹码量
            /// </summary>
            public uint Chips { get; set; }

            /// <summary>
            /// 玩家名
            /// </summary>
            public string PlayerName { get; set; }

            /// <summary>
            /// 本阶段中，总共下注的筹码量
            /// </summary>
            public uint StageBet { get; set; }

            /// <summary>
            /// 本轮中，下注的筹码量
            /// </summary>
            public uint RoundBet { get; set; }

            /// <summary>
            /// 该玩家上次进行的操作
            /// </summary>
            public PlayerOperation LastOperation { get; set; }

            /// <summary>
            /// 是否盖牌
            /// </summary>
            public bool IsFold { get; set; }

            /// <summary>
            /// 是否AllIn
            /// </summary>
            public bool IsAllIn { get; set; }

            /// <summary>
            /// 是否断开连接
            /// </summary>
            public bool IsDisconnected { get; set; }
        }
    }
}
