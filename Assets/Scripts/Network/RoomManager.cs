using Framework.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Network
{
    public class RoomManager
    {
        /// <summary>
        /// 自动编号
        /// </summary>
        private static int Number = 0;

        private RoomList _roomList = new RoomList();
        public RoomList RoomList => _roomList;

        public RoomInfo CreateRoom(string owner)
        {
            if (WhichRoom(owner, out _))
            {
                Log.WriteWarn($"Player {owner} attempted to create a second room association.");
                return null;
            }

            var roomInfo = RoomInfo.CreateRoom(owner);
            roomInfo.Id = Number++;
            _roomList.Rooms.Add(roomInfo);
            Log.Write($"Create a new room owned by {owner}, which id is {roomInfo.Id}.");
            return roomInfo;
        }
        
        /// <summary>
        /// 找到对应Id的房间
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns>房间信息，如果没找到则返回null</returns>
        public RoomInfo FindRoom(int roomId)
        {
            return _roomList.Rooms.Find(room => room.Id == roomId);
        }

        /// <summary>
        /// 加入房间
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="player"></param>
        /// <returns> 0 = success, -1 = room doesn't exist, -2 = already associated with a room, -3 = full room </returns>
        public int Join(int roomId, string player)
        {
            var room = FindRoom(roomId);
            
            if (room == null)
            {
                Log.WriteWarn($"Player {player} requested to join the room {roomId} but the room doesn't exist.");
                return -1;
            }
            room.EnsureSeatModel();
            if (WhichRoom(player, out _))
            {
                Log.WriteWarn($"Player {player} requested to join room {roomId} while already associated with a room.");
                return -2;
            }

            var seatIndex = room.FindAvailableSeatIndex();
            if (seatIndex < 0)
            {
                Log.Write($"Player {player} requested to join the room {roomId} but the room is full.");
                return -3;
            }

            room.Seats.Add(new RoomSeatInfo
            {
                SeatIndex = seatIndex,
                PlayerName = player,
                IsReady = false,
                State = room.IsWaiting ? RoomSeatState.Waiting : RoomSeatState.WaitingForNextHand,
            });
            room.SynchronizeLegacyLists();

            Log.Write($"Player {player} joined room {roomId} at seat {seatIndex}.");
            return 0;
        }

        /// <summary>
        /// 恢复上次房间关联。返回 0 表示已入座，1 表示作为旁观成员返回。
        /// </summary>
        public int Restore(int roomId, string player, bool canTakeSeat)
        {
            var room = FindRoom(roomId);
            if (room == null)
                return -1;

            room.EnsureSeatModel();
            if (WhichRoom(player, out var associatedRoomId) && associatedRoomId != roomId)
                return -2;

            var existingSeat = room.FindSeat(player);
            if (existingSeat != null)
                return 0;

            if (!room.Spectators.Contains(player))
                room.Spectators.Add(player);

            var seatIndex = canTakeSeat ? room.FindAvailableSeatIndex() : -1;
            if (seatIndex < 0)
            {
                room.SynchronizeLegacyLists();
                return 1;
            }

            room.Spectators.Remove(player);
            room.Seats.Add(new RoomSeatInfo
            {
                SeatIndex = seatIndex,
                PlayerName = player,
                IsReady = false,
                State = room.IsWaiting ? RoomSeatState.Waiting : RoomSeatState.WaitingForNextHand,
            });
            room.SynchronizeLegacyLists();
            return 0;
        }

        public bool MoveToSpectator(int roomId, string player)
        {
            var room = FindRoom(roomId);
            if (room == null)
                return false;

            room.EnsureSeatModel();
            var seat = room.FindSeat(player);
            if (seat == null)
                return room.Spectators.Contains(player);

            room.Seats.Remove(seat);
            if (!room.Spectators.Contains(player))
                room.Spectators.Add(player);
            TransferOwnerIfNeeded(room, player);
            room.SynchronizeLegacyLists();
            return true;
        }

        public int ReleaseWatchingSeats(int roomId)
        {
            var room = FindRoom(roomId);
            if (room == null)
                return 0;

            room.EnsureSeatModel();
            var released = room.Seats
                .Where(seat => seat.State == RoomSeatState.Watching)
                .ToList();
            foreach (var seat in released)
            {
                room.Seats.Remove(seat);
                if (!room.Spectators.Contains(seat.PlayerName))
                    room.Spectators.Add(seat.PlayerName);
                TransferOwnerIfNeeded(room, seat.PlayerName);
            }

            room.SynchronizeLegacyLists();
            return released.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="player"></param>
        /// <returns> 0 = success, -1 = room doesn't exist, -2 = player doesn't exist</returns>
        public int Leave(int roomId, string player, bool deferRemoval = false)
        {
            var room = FindRoom(roomId);
            if (room == null)
            {
                Log.WriteWarn($"Player {player} requested to leave the room {roomId} but the room doesn't exist.");
                return -1;
            }

            room.EnsureSeatModel();
            var seat = room.Seats.Find(candidate => candidate.PlayerName == player);
            var isSpectator = room.Spectators.Remove(player);
            if (seat == null && !isSpectator)
            {
                Log.WriteWarn($"The player {player} requested to leave the room but the player is not in the room {roomId}!");
                return -2;
            }

            if (seat != null && deferRemoval && !room.IsWaiting)
            {
                seat.State = RoomSeatState.Leaving;
                seat.IsReady = false;
            }
            else if (seat != null)
            {
                room.Seats.Remove(seat);
            }
            Log.Write($"Player {player} left the room {roomId}.");

            TransferOwnerIfNeeded(room, player);
            room.SynchronizeLegacyLists();
            if (room.MemberCount == 0)
                DeleteRoom(roomId);
            return 0;
        }

        public bool FinalizeDepartures(int roomId)
        {
            var room = FindRoom(roomId);
            if (room == null)
                return false;

            room.EnsureSeatModel();
            var departingOwner = room.Seats.Any(seat =>
                seat.State == RoomSeatState.Leaving && seat.PlayerName == room.OwnerName);
            room.Seats.RemoveAll(seat => seat.State == RoomSeatState.Leaving);
            if (departingOwner)
                TransferOwnerIfNeeded(room, room.OwnerName);
            room.SynchronizeLegacyLists();
            if (room.MemberCount > 0)
                return true;

            DeleteRoom(roomId);
            return false;
        }

        /// <summary>
        /// 玩家更改准备状态（已准备则转为未准备，未准备则转为已准备）
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="playerName"></param>
        /// <returns>0 = success, -1 = room doesn't exist, -2 = player not in room</returns>
        public int PlayerChangeReady(int roomId, string playerName)
        {
            var room = FindRoom(roomId);
            if (room == null)
            {
                Log.WriteWarn($"Player {playerName} requested to ready in room {roomId} but the room doesn't exist.");
                return -1;
            }
            if (!room.IsWaiting)
                return -3;

            room.EnsureSeatModel();
            var seat = room.Seats.Find(candidate =>
                candidate.PlayerName == playerName && candidate.State != RoomSeatState.Leaving);
            if (seat != null)
            {
                seat.IsReady = !seat.IsReady;
                room.SynchronizeLegacyLists();
                if (seat.IsReady)
                    Log.Write($"Player {playerName} in room {roomId} readied.");
                else
                    Log.Write($"Player {playerName} in room {roomId} unreadied.");
                return 0;
            }

            // 房间里没有找到此玩家
            Log.WriteWarn($"Player {playerName} requested to ready in room {roomId} but the player is not in the room.");
            return -2;
        }

        /// <summary>
        /// 手动开始一个处于大厅等待状态的房间
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns>0 = success, -1 = room doesn't exist, -2 = player not readied, -3 = owner not in the room, -5 = too few player()</returns>
        public int StartGame(int roomId)
        {
            var room = FindRoom(roomId);
            if (room == null)
            {
                Log.WriteWarn($"The room {roomId} doesn't exist when starting the game!");
                return -1;
            }

            if(room.PlayerCount < 2)
            {
                Log.WriteWarn($"The room {room.Id} has too few player to start the game!");
                return -5;
            }

            if (!room.IsWaiting)
            {
                Log.WriteWarn($"The game in room {room.Id} has already started.");
                return -6;
            }

            room.EnsureSeatModel();
            var ownerSeat = room.Seats.Find(seat =>
                seat.PlayerName == room.OwnerName && seat.State != RoomSeatState.Leaving);
            if (ownerSeat == null)
            {
                Log.WriteWarn($"The owner of room {room.Id} called {room.OwnerName} is not in the room!");
                return -3;
            }


            foreach (var seat in room.Seats.Where(seat => seat.State != RoomSeatState.Leaving))
            {
                // 房主不考虑是否准备
                if (seat == ownerSeat)
                    continue;
                if (!seat.IsReady)
                    return -2;
            }

            // 开始游戏，不再等待
            room.IsWaiting = false;
            foreach (var seat in room.Seats.Where(seat => seat.State != RoomSeatState.Leaving))
                seat.State = RoomSeatState.WaitingForNextHand;
            room.SynchronizeLegacyLists();
            Log.Write($"The game in room {roomId} is started.");
            string log = $"Containing {room.PlayerCount}: ";
            foreach(var player in room.Players)
                log += player.ToString() + ", ";
            Log.Write(log);
            return 0;
        }

        public void ReturnToWaiting(int roomId)
        {
            var room = FindRoom(roomId);
            if (room == null)
                return;

            room.IsWaiting = true;
            room.EnsureSeatModel();
            foreach (var seat in room.Seats.Where(seat => seat.State != RoomSeatState.Leaving))
            {
                seat.State = RoomSeatState.Waiting;
                seat.IsReady = false;
            }
            room.SynchronizeLegacyLists();
        }

        public bool ResizeSeats(int roomId, int maxPlayerCount)
        {
            var room = FindRoom(roomId);
            if (room == null || !room.IsWaiting || maxPlayerCount < 2 || maxPlayerCount < room.PlayerCount)
                return false;

            room.EnsureSeatModel();
            var occupiedSeats = room.Seats
                .Where(seat => seat.State != RoomSeatState.Leaving)
                .OrderBy(seat => seat.SeatIndex)
                .ToList();
            for (int index = 0; index < occupiedSeats.Count; index++)
                occupiedSeats[index].SeatIndex = index;
            room.MaxPlayerCount = maxPlayerCount;
            room.SynchronizeLegacyLists();
            return true;
        }

        /// <summary>
        /// 此玩家在哪个房间中
        /// </summary>
        /// <param name="playerName">玩家名</param>
        /// <param name="roomId">查找得出的房间id</param>
        /// <returns>是否在房间中</returns>
        public bool WhichRoom(string playerName, out int roomId)
        {
            foreach(var room in RoomList.Rooms)
            {
                room.EnsureSeatModel();
                if (room.ContainsMember(playerName))
                {
                    roomId = room.Id;
                    return true;
                }
            }
            roomId = -1;
            return false;
        }

        public void DeleteRoom(int roomId)
        {
            var room = FindRoom(roomId);
            if (room == null)
                return;
            _roomList.Rooms.Remove(room);
            Log.Write($"Room {roomId} is deleted.");
        }

        private static void TransferOwnerIfNeeded(RoomInfo room, string departingPlayer)
        {
            if (room.OwnerName != departingPlayer && room.ContainsMember(room.OwnerName))
            {
                return;
            }

            room.OwnerName = room.Seats
                .Where(seat => seat.State != RoomSeatState.Leaving)
                .OrderBy(seat => seat.SeatIndex)
                .Select(seat => seat.PlayerName)
                .Concat(room.Spectators)
                .FirstOrDefault();
        }
    }
}
