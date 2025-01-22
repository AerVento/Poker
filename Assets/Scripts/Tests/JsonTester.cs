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
            TestRoundResultDeserialize();

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

        public void TestRoundResultSerialize()
        {
            var deck = Poker.RandomDeck();
            var item = new RoundResult()
            {
                PlayerResults = new List<RoundResult.Player>()
                {
                    new RoundResult.Player()
                    {
                        PlayerName = "yurzhang",
                        IsFold = true,
                        Hole = new List<Poker>() { deck.Dequeue(), deck.Dequeue()},
                        ChipEarned = 0
                    },
                    new RoundResult.Player()
                    {
                        PlayerName = "kakie",
                        IsFold = false,
                        Hole = new List<Poker>() { deck.Dequeue(), deck.Dequeue()},
                        ChipEarned = 12
                    },
                    new RoundResult.Player()
                    {
                        PlayerName = "aervento",
                        IsFold = true,
                        Hole = new List<Poker>() { deck.Dequeue(), deck.Dequeue()},
                        ChipEarned = 50
                    },
                    new RoundResult.Player()
                    {
                        PlayerName = "schoparks",
                        IsFold = false,
                        Hole = new List<Poker>() { deck.Dequeue(), deck.Dequeue()},
                        ChipEarned = 500
                    }
                }
            };
            string result = JsonSerializer.Serialize(item);
            Debug.Log(result);
        }

        public void TestRoundResultDeserialize()
        {
            string result = "{\"PlayerResults\":[{\"PlayerName\":\"yurzhang\",\"IsFold\":true,\"Hole\":[{\"Suit\":0,\"Rank\":12},{\"Suit\":1,\"Rank\":4}],\"ChipEarned\":0},{\"PlayerName\":\"kakie\",\"IsFold\":false,\"Hole\":[{\"Suit\":0,\"Rank\":6},{\"Suit\":3,\"Rank\":1}],\"ChipEarned\":12},{\"PlayerName\":\"aervento\",\"IsFold\":true,\"Hole\":[{\"Suit\":0,\"Rank\":1},{\"Suit\":1,\"Rank\":3}],\"ChipEarned\":50},{\"PlayerName\":\"schoparks\",\"IsFold\":false,\"Hole\":[{\"Suit\":2,\"Rank\":10},{\"Suit\":1,\"Rank\":7}],\"ChipEarned\":500}]}";
            RoundResult item = JsonSerializer.Deserialize<RoundResult>(result);
            Debug.Log(result);
        }
    }
}
