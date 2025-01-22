using Framework.States;
using Framework.Timer;
using Mirror.BouncyCastle.Bcpg.OpenPgp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Game.Network
{

    /// <summary>
    /// 游戏阶段
    /// </summary>
    public enum GameStage
    {
        /// <summary>
        /// 翻牌前的轮次
        /// </summary>
        Preflop,

        /// <summary>
        /// 翻牌发出后的轮次
        /// </summary>
        Flop,

        /// <summary>
        /// 转牌发出后的轮次
        /// </summary>
        Turn,

        /// <summary>
        /// 河牌发出后的轮次
        /// </summary>
        River,

        /// <summary>
        /// 一轮结束
        /// </summary>
        RoundOver,

        /// <summary>
        /// 游戏结束
        /// </summary>
        GameOver,
    }
    /// <summary>
    /// 服务器管理的游戏信息
    /// 比客户端收到的游戏信息还多一些内容
    /// </summary>
    [System.Serializable]
    public class ServerGameInfo
    {

        public GameStage Stage { get; set; }

        /// <summary>
        /// 当前使用的扑克牌序列
        /// </summary>
        [JsonIgnore]
        public Queue<Poker> Deck { get; set; }

        public RoomInfo RoomInfo { get; set; }

        /// <summary>
        /// 思考Timer
        /// </summary>
        public Timer Timer { get; set; }
        
        public List<PlayerInfo> Players { get; set; } = new List<PlayerInfo>();
        
        [JsonIgnore]
        public int PlayersCount => RoomInfo.Players.Count;

        /// <summary>
        /// 本轮次中新AllIn的选手下标，轮次结束后根据这个分割奖池。
        /// </summary>
        public List<int> NewAllInPlayers { get; set; } = new List<int>();

        /// <summary>
        /// 本轮次中新Fold的选手的下标，轮次结束后根据这个分割奖池。
        /// </summary>
        public List<int> NewFoldPlayers { get; set; } = new List<int>();

        /// <summary>
        /// 总奖池金额
        /// </summary>
        public uint TotalPot { get; set; } = 0;

        /// <summary>
        /// 主池、分池之间的金额
        /// </summary>
        public Stack<Pot> Pots { get; set; } = new Stack<Pot>();

        /// <summary>
        /// 庄家位置
        /// </summary>
        public int Dealer { get; set; } = 0;
        
        /// <summary>
        /// 小盲位置
        /// </summary>
        public int SmallBlind { get; set; } = 1;

        /// <summary>
        /// 大盲位置
        /// </summary>
        public int BigBlind { get; set; } = 2;

        public bool BigBlindRaise { get; set; } = true;

        /// <summary>
        /// 在每一轮次中，起始操作玩家的位置。
        /// 如果中途有人Raise/Allin，那么起始操作的玩家会变为Raise/Allin的人。
        /// </summary>
        public int FirstBet { get; set; } = 1;

        /// <summary>
        /// 正在操作的玩家
        /// </summary>
        public int OperatingPlayer { get; set; } = 1;

        /// <summary>
        /// 公共牌
        /// </summary>
        public List<Poker> Community { get; set; } = new List<Poker>();

        /// <summary>
        /// 上轮中最高加注的筹码量
        /// </summary>
        public uint LastHighestRaise { get; set; } = 0;

        /// <summary>
        /// 此轮中最高加注的筹码量
        /// </summary>
        public uint CurrentHighestRaise { get; set; } = 0;

        /// <summary>
        /// 加注时最小加注量
        /// </summary>
        [JsonIgnore]
        public uint LeastRaise => System.Math.Max(LastHighestRaise, CurrentHighestRaise);

        /// <summary>
        /// 此轮中下注最高的玩家下注的数量
        /// </summary>
        public uint CurrentHighestPlayerBet { get; set; } = 0;

        public static ServerGameInfo Create(RoomInfo roomInfo)
        {
            ServerGameInfo info = new ServerGameInfo();
            info.RoomInfo = roomInfo;
            info.Timer = new Timer(roomInfo.ThinkingTime);
            foreach(var player in roomInfo.Players)
            {
                info.Players.Add(new PlayerInfo()
                { 
                    Chips = System.Convert.ToUInt32(roomInfo.InitialChips),
                    PlayerName = player,
                });
            }
            
            // 游戏开始时，随机一位成为小盲，后一位成为大盲，然后让大盲的下一个称为第一个进行操作的玩家
            Random r = new Random();
            info.Dealer = r.Next(0, info.RoomInfo.PlayerCount);
            info.SmallBlind = (info.Dealer + 1) % info.RoomInfo.PlayerCount;
            info.BigBlind = (info.SmallBlind + 1) % info.RoomInfo.PlayerCount;
            info.OperatingPlayer = (info.BigBlind + 1) % info.RoomInfo.PlayerCount;

            return info;
        }

        [System.Serializable]
        public class PlayerInfo
        {

            /// <summary>
            /// 筹码量
            /// </summary>
            public uint Chips { get; set; } = 0;

            /// <summary>
            /// 玩家名
            /// </summary>
            public string PlayerName { get; set; } = "Unknown";

            /// <summary>
            /// 是否盖牌
            /// </summary>
            public bool IsFold { get; set; }

            /// <summary>
            /// 是否AllIn
            /// </summary>
            public bool IsAllIn { get; set; }

            /// <summary>
            /// 是否离线
            /// </summary>
            public bool IsDisconnected { get; set; } = false;

            /// <summary>
            /// 本阶段中，总共下注的筹码量
            /// </summary>
            public uint StageBet { get; set; } = 0;

            /// <summary>
            /// 本轮中，下注的筹码量
            /// </summary>
            public uint RoundBet { get; set; } = 0;

            /// <summary>
            /// 该玩家上次进行的操作
            /// </summary>
            public PlayerOperation LastOperation { get; set; }


            /// <summary>
            /// 底牌
            /// </summary>
            public List<Poker> Hole { get; set; } = new List<Poker>();
        }

        /// <summary>
        /// 奖池
        /// </summary>
        [System.Serializable]
        public class Pot
        {
            /// <summary>
            /// 参与奖池分配的玩家下标，从小到大排序
            /// </summary>
            public List<int> JoinedPlayers { get; set; } = new List<int>();

            /// <summary>
            /// 奖池金额
            /// </summary>
            public uint ChipCount { get; set; } = 0;
        }
    }
}
