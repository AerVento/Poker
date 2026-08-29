using Framework.States;
using Framework.Timer;
using Game.Network.Accounts;
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

        [JsonIgnore]
        public string HandId { get; set; }

        [JsonIgnore]
        public long ActionSequence { get; set; }

        /// <summary>
        /// 思考Timer
        /// </summary>
        public Timer Timer { get; set; }
        
        public List<PlayerInfo> Players { get; set; } = new List<PlayerInfo>();
        
        [JsonIgnore]
        public int PlayersCount => RoomInfo.Players.Count;

        /// <summary>
        /// 总奖池金额
        /// </summary>
        public uint TotalPot { get; set; } = 0;

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

        /// <summary>
        /// 上一手牌的参赛人数，用于三人桌转入单挑时避免同一玩家连续承担大盲。
        /// </summary>
        [JsonIgnore]
        public int LastHandParticipantCount { get; set; }

        /// <summary>
        /// 正在操作的玩家
        /// </summary>
        public int OperatingPlayer { get; set; } = 1;

        /// <summary>
        /// 公共牌
        /// </summary>
        public List<Poker> Community { get; set; } = new List<Poker>();

        /// <summary>
        /// 当前下注街最后一次完整下注或加注的增量。
        /// </summary>
        public uint CurrentHighestRaise { get; set; } = 0;

        /// <summary>
        /// 加注时最小加注量
        /// </summary>
        [JsonIgnore]
        public uint LeastRaise => CurrentHighestRaise;

        /// <summary>
        /// 此轮中下注最高的玩家下注的数量
        /// </summary>
        public uint CurrentHighestPlayerBet { get; set; } = 0;

        public bool CanPlayerRaise(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= Players.Count)
                return false;

            var player = Players[playerIndex];
            if (player.IsFold || player.IsAllIn || player.IsDisconnected)
                return false;

            return HoldemRules.CanRaise(
                player.HasActedThisStage,
                player.BetFacedAtLastAction,
                CurrentHighestPlayerBet,
                CurrentHighestRaise);
        }

        public static ServerGameInfo Create(
            RoomInfo roomInfo,
            IReadOnlyList<PlayerAccount> accounts)
        {
            if (accounts == null || accounts.Count != roomInfo.PlayerCount)
                throw new ArgumentException("Every room player must have an account snapshot.", nameof(accounts));

            ServerGameInfo info = new ServerGameInfo();
            info.RoomInfo = roomInfo;
            info.Timer = new Timer(roomInfo.ThinkingTime);
            for (int index = 0; index < roomInfo.Players.Count; index++)
            {
                var player = roomInfo.Players[index];
                var account = accounts[index];
                if (account.UserName != player)
                    throw new ArgumentException("Account snapshots must follow room player order.", nameof(accounts));

                info.Players.Add(new PlayerInfo()
                {
                    AccountId = account.AccountId,
                    Chips = account.Chips,
                    PlayerName = player,
                });
            }
            
            // 游戏开始时随机选择按钮；StartNew 会按照仍在参赛的玩家重新计算盲注和行动位置。
            Random r = new Random();
            info.Dealer = r.Next(0, info.RoomInfo.PlayerCount);

            return info;
        }

        [System.Serializable]
        public class PlayerInfo
        {

            [JsonIgnore]
            public string AccountId { get; set; }

            [JsonIgnore]
            public uint HandStartingChips { get; set; }

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
            /// 是否已在当前下注街采取过行动。强制盲注不算行动。
            /// </summary>
            [JsonIgnore]
            public bool HasActedThisStage { get; set; }

            /// <summary>
            /// 玩家上次行动时所面对的最高下注额，用于判断短码 all-in 是否累计到完整加注并重新开放行动。
            /// </summary>
            [JsonIgnore]
            public uint BetFacedAtLastAction { get; set; }


            /// <summary>
            /// 底牌
            /// </summary>
            public List<Poker> Hole { get; set; } = new List<Poker>();
        }
    }
}
