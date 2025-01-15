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
        /// <returns> 0 = success, -1 = room doesn't exist, -2 = already in the room, -3 = full room </returns>
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
            
            room.Players.Add(player);

            Log.Write($"Player {player} joined the room {roomId}.");
            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="player"></param>
        /// <returns> 0 = success, -1 = room doesn't exist</returns>
        public int Leave(int roomId, string player)
        {
            var room = FindRoom(roomId);
            if (room == null)
            {
                Log.WriteWarn($"Player {player} requested to leave the room {roomId} but the room doesn't exist.");
                return -1;
            }

            room.Players.Remove(player);
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
        /// 开始一个房间的游戏
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns>正常开始返回true，房间不存在或还有玩家没准备则返回false</returns>
        public bool StartGame(int roomId)
        {
            var room = FindRoom(roomId);
            if (room == null)
                return false;
            foreach (var isReady in room.ReadyStatus)
                if (!isReady)
                    return false;
            // 开始游戏，不再等待
            room.IsWaiting = false;
            Log.Write($"The game in room {roomId} is started.");
            string log = $"Containing {room.PlayerCount}: ";
            foreach(var player in room.Players)
                log += player.ToString() + ", ";
            Log.Write(log);
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
