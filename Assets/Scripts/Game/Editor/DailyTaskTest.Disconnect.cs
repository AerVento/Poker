using Game.Network;
using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public partial class DailyTaskTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void WaitingRoomDisconnectLeavesAndClearsBookmark(bool lastMember)
        {
            var go = new GameObject("Waiting disconnect test");
            go.SetActive(false);
            try
            {
                var network = go.AddComponent<NetworkRoom>();
                var players = Field<PlayerManager>(network, "_playerManager");
                var rooms = Field<RoomManager>(network, "_roomManager");
                var games = Field<GameManager>(network, "_gameManager");
                Set(network, "_departures", new RoomDepartureService(rooms, games,
                    name => _repository.SetLastRoom(name, null)));
                var room = rooms.CreateRoom("Alice");
                if (!lastMember) rooms.Join(room.Id, "Bob");
                _repository.SetLastRoom("Alice", room.Id);
                var connection = new NetworkConnectionToClient(91) { isAuthenticated = true };
                players.BindAccount(_repository.FindById("Alice"), connection);
                network.OnServerDisconnect(connection);
                Assert.IsFalse(rooms.WhichRoom("Alice", out _));
                Assert.IsNull(_repository.FindById("Alice").LastRoomId);
                Assert.IsFalse(room.Spectators.Contains("Alice"));
                if (lastMember) Assert.IsNull(rooms.FindRoom(room.Id));
                else Assert.IsTrue(rooms.FindRoom(room.Id).ContainsMember("Bob"));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
