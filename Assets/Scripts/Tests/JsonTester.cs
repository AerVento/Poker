using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Game.Network;
using UnityEngine;

namespace Game.Tests
{
    public class JsonTester : MonoBehaviour
    {

        private void Start()
        {
            TestUnionSerialize();

        }
        public void TestRoomInfoSerialize() 
        {
            RoomInfo room = RoomInfo.CreateRoom("AerVento");
            string result = JsonSerializer.Serialize(room);
            Debug.Log(result);
        }

        public void TestRoomInfoDeserialize()
        {
            string result = "{\"Id\":0,\"MaxPlayerCount\":3,\"OwnerName\":\"AerVento\",\"Players\":[\"AerVento\",\"yurzhang\"],\"InitialChips\":100,\"ThinkingTime\":20}";
            RoomInfo room = JsonSerializer.Deserialize<RoomInfo>(result);
        }

        public void TestRoomListSerialize()
        {
            RoomList list = new RoomList();
            list.Rooms.Add(RoomInfo.CreateRoom("AerVento"));
            list.Rooms.Add(RoomInfo.CreateRoom("yurzhang"));
            string result = JsonSerializer.Serialize(list);
            Debug.Log(result);
        }

        public void TestRoomListDeserialize()
        {
            string result = "{\"Rooms\":[{\"Id\":0,\"MaxPlayerCount\":8,\"OwnerName\":\"AerVento\",\"Players\":[\"AerVento\"],\"InitialChips\":300,\"ThinkingTime\":30},{\"Id\":0,\"MaxPlayerCount\":4,\"OwnerName\":\"yurzhang\",\"Players\":[\"yurzhang\"],\"InitialChips\":300,\"ThinkingTime\":30}]}";
            RoomList list = JsonSerializer.Deserialize<RoomList>(result);
        }

        public void TestUnionSerialize()
        {
            List<(int, string)> list = new List<(int, string)>()
            {
                (1, "a"),
                (2, "b"),
                (3, "c"),
                (4, "d")
            };
            string result = JsonSerializer.Serialize(list);
            Debug.Log(result);
        }

        public void TestUnionDeserialize()
        {
            string result = "";
            List<(int, string)> list = JsonSerializer.Deserialize<List<(int, string)>>(result);
        }
    }
}
