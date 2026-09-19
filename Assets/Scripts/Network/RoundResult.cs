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
        public int RoomId { get; set; }
        public string HandId { get; set; }
        public string RevealPlayerName { get; set; }
        public bool IsRevealed { get; set; }

        public List<Poker> Community { get; set; } = new List<Poker>();
        public List<Player> PlayerResults { get; set; } = new List<Player>();
        public RoundResult ForSpectator()
        {
            return new RoundResult
            {
                RoomId = RoomId,
                HandId = HandId,
                RevealPlayerName = RevealPlayerName,
                IsRevealed = IsRevealed,
                Community = new List<Poker>(Community),
                PlayerResults = PlayerResults.Select(player => new Player
                {
                    PlayerName = player.PlayerName,
                    IsFold = player.IsFold,
                    IsDisconnected = player.IsDisconnected,
                    // CompleteRound already includes only publicly revealed showdown cards.
                    Hole = player.IsFold ? new List<Poker>() : new List<Poker>(player.Hole),
                    ChipEarned = player.ChipEarned,
                    BestRank = player.IsFold ? null : player.BestRank,
                    BestGrades = player.IsFold ? new List<int>() : new List<int>(player.BestGrades),
                    BestHand = player.IsFold ? new List<Poker>() : new List<Poker>(player.BestHand),
                }).ToList(),
            };
        }

        [System.Serializable]
        public class Player
        {
            public string PlayerName { get; set; }
            public bool IsFold { get; set; }
            public bool IsDisconnected { get; set; }
            public List<Poker> Hole { get; set; } = new List<Poker>();
            public uint ChipEarned { get; set; }
            public PokerHandRank? BestRank { get; set; }
            public List<int> BestGrades { get; set; } = new List<int>();
            public List<Poker> BestHand { get; set; } = new List<Poker>();
        }
    }
}
