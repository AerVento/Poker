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
        /// <returns> 0 = success, -1 = room doesn't exist, -2 = already in the room, -3 = full room, -4 = game started </returns>
        public int Join(int roomId, string player)
        {
            var room = FindRoom(roomId);
            
            if (room == null)
            {
                Log.WriteWarn($"Player {player} requested to join the room {roomId} but the room doesn't exist.");
                return -1;
            }
            
            if(room.Players.Contains(player))
            {
                Log.WriteWarn($"Player {player} requested to join the room {roomId} but the player is already in the room.");
                return -2;
            }

            if(room.PlayerCount >= room.MaxPlayerCount)
            {
                Log.Write($"Player {player} requested to join the room {roomId} but the room is full.");
                return -3;
            }

            if (!room.IsWaiting)
            {
                Log.Write($"Player {player} requested to join the room {roomId} but the game is already started.");
                return -4;
            }

            room.Players.Add(player);
            room.ReadyStatus.Add(false);

            Log.Write($"Player {player} joined the room {roomId}.");
            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="player"></param>
        /// <returns> 0 = success, -1 = room doesn't exist, -2 = player doesn't exist</returns>
        public int Leave(int roomId, string player)
        {
            var room = FindRoom(roomId);
            if (room == null)
            {
                Log.WriteWarn($"Player {player} requested to leave the room {roomId} but the room doesn't exist.");
                return -1;
            }

            int index = room.Players.IndexOf(player);
            if (index == -1)
            {
                Log.WriteWarn($"The player {player} requested to leave the room but the player is not in the room {roomId}!");
                return -2;
            }

            room.Players.RemoveAt(index);
            room.ReadyStatus.RemoveAt(index);
            Log.Write($"Player {player} left the room {roomId}.");
            
            if (room.OwnerName != player)
                return 0; // 短路，如果不是owner则不进行下面的逻辑
            
            if (room.PlayerCount > 0)
                room.OwnerName = room.Players[0];
            else
                DeleteRoom(roomId);
            return 0;
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

            for(int i = 0; i <  room.Players.Count; i++)
            {
                var player = room.Players[i];
                if(player == playerName)
                {
                    room.ReadyStatus[i] = !room.ReadyStatus[i];
                    if (room.ReadyStatus[i])
                        Log.Write($"Player {playerName} in room {roomId} readied.");
                    else
                        Log.Write($"Player {playerName} in room {roomId} unreadied.");
                    return 0;
                }
            }

            // 房间里没有找到此玩家
            Log.WriteWarn($"Player {playerName} requested to ready in room {roomId} but the player is not in the room.");
            return -2;
        }

        /// <summary>
        /// 开始一个房间的游戏，房间开始后不能再加入
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

            int index = room.Players.IndexOf(room.OwnerName);
            if (index == -1)
            {
                Log.WriteWarn($"The owner of room {room.Id} called {room.OwnerName} is not in the room!");
                return -3;
            }


            for(int i = 0; i < room.ReadyStatus.Count; i++)
            {
                // 房主不考虑是否准备
                if (i == index)
                    continue;
                if (!room.ReadyStatus[i])
                    return -2;
            }

            // 开始游戏，不再等待
            room.IsWaiting = false;
            Log.Write($"The game in room {roomId} is started.");
            string log = $"Containing {room.PlayerCount}: ";
            foreach(var player in room.Players)
                log += player.ToString() + ", ";
            Log.Write(log);
            return 0;
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
                if(room.Players.Contains(playerName))
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
            _roomList.Rooms.Remove(room);
            Log.Write($"Room {roomId} is deleted.");
        }
    }
}
