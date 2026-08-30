using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using UnityEngine;

namespace Game.Network
{
    public enum RoomSeatState
    {
        Waiting,
        WaitingForNextHand,
        Playing,
        DisconnectedInHand,
        Watching,
        Leaving,
    }

    [System.Serializable]
    public sealed class RoomSeatInfo
    {
        public int SeatIndex { get; set; }

        public string PlayerName { get; set; }

        public bool IsReady { get; set; }

        public RoomSeatState State { get; set; }
    }

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
        /// 权威座位列表。SeatIndex 在玩家留在房间期间保持稳定，列表本身只保存已占用或待释放的座位。
        /// </summary>
        public List<RoomSeatInfo> Seats { get; set; } = new List<RoomSeatInfo>();

        /// <summary>
        /// 房间旁观成员不占用参赛座位，但仍保留房间关联并接收公开牌局状态。
        /// </summary>
        public List<string> Spectators { get; set; } = new List<string>();

        /// <summary>
        /// 初始筹码
        /// </summary>
        public int InitialChips { get; set; } = 300;

        /// <summary>
        /// 小盲注
        /// </summary>
        public int SmallBlindChips { get; set; } = 1;

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

        [JsonIgnore]
        public int MemberCount => PlayerCount + (Spectators?.Count ?? 0);

        [JsonIgnore]
        public IEnumerable<string> MemberNames => Players.Concat(Spectators ?? Enumerable.Empty<string>());

        public void EnsureSeatModel()
        {
            if (Seats == null)
                Seats = new List<RoomSeatInfo>();
            if (Spectators == null)
                Spectators = new List<string>();

            if (Seats.Count == 0 && Players != null)
            {
                for (int index = 0; index < Players.Count; index++)
                {
                    Seats.Add(new RoomSeatInfo
                    {
                        SeatIndex = index,
                        PlayerName = Players[index],
                        IsReady = ReadyStatus != null && index < ReadyStatus.Count && ReadyStatus[index],
                        State = IsWaiting ? RoomSeatState.Waiting : RoomSeatState.WaitingForNextHand,
                    });
                }
            }

            SynchronizeLegacyLists();
        }

        public RoomSeatInfo FindSeat(string playerName)
        {
            EnsureSeatModel();
            return Seats.Find(seat => seat.PlayerName == playerName);
        }

        public bool ContainsMember(string playerName)
        {
            EnsureSeatModel();
            return Seats.Any(seat => seat.PlayerName == playerName) || Spectators.Contains(playerName);
        }

        public RoomSeatInfo FindSeat(int seatIndex)
        {
            EnsureSeatModel();
            return Seats.Find(seat => seat.SeatIndex == seatIndex);
        }

        public int FindAvailableSeatIndex()
        {
            EnsureSeatModel();
            for (int seatIndex = 0; seatIndex < MaxPlayerCount; seatIndex++)
            {
                if (Seats.All(seat => seat.SeatIndex != seatIndex))
                    return seatIndex;
            }

            return -1;
        }

        public void SynchronizeLegacyLists()
        {
            if (Players == null)
                Players = new List<string>();
            if (ReadyStatus == null)
                ReadyStatus = new List<bool>();

            Players.Clear();
            ReadyStatus.Clear();
            if (Seats == null)
                return;

            foreach (var seat in Seats
                .Where(seat => seat != null && seat.State != RoomSeatState.Leaving)
                .OrderBy(seat => seat.SeatIndex))
            {
                Players.Add(seat.PlayerName);
                ReadyStatus.Add(seat.IsReady);
            }
        }

        public static RoomInfo CreateRoom(string owner) 
        {
            RoomInfo room = new RoomInfo();
            room.OwnerName = owner;
            room.Seats.Add(new RoomSeatInfo
            {
                SeatIndex = 0,
                PlayerName = owner,
                IsReady = false,
                State = RoomSeatState.Waiting,
            });
            room.SynchronizeLegacyLists();
            return room;
        }
    }
}

