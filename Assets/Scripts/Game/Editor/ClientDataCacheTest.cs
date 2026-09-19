using Game.Network;
using Game.Network.Accounts;
using Mirror;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ClientDataCacheTest
    {
        [TestCase(600d)]
        [TestCase(60d)]
        public void CacheExpiresAtBoundaryAndReadsDoNotExtendLifetime(double lifetime)
        {
            var cache = new ClientSnapshotCache<string>(lifetime);
            Assert.IsFalse(cache.TryGet(0, out _));
            cache.Store("first", 20);
            Assert.IsTrue(cache.TryGet(20 + lifetime - 0.001, out var value));
            Assert.AreEqual("first", value);
            Assert.IsFalse(cache.TryGet(20 + lifetime, out _));
            cache.Store("updated", 40 + lifetime);
            Assert.IsTrue(cache.TryGet(40 + lifetime, out value));
            Assert.AreEqual("updated", value);
            cache.Clear();
            Assert.IsFalse(cache.TryGet(40 + lifetime, out _));
        }

        [Test]
        public void RefreshingOneLeaderboardDoesNotRefreshTheOther()
        {
            var wealth = new ClientSnapshotCache<int>(60);
            var service = new ClientSnapshotCache<int>(60);
            wealth.Store(1, 0);
            service.Store(2, 30);
            Assert.IsFalse(wealth.TryGet(60, out _));
            Assert.IsTrue(service.TryGet(60, out var value));
            Assert.AreEqual(2, value);
            wealth.Store(3, 75);
            Assert.IsFalse(service.TryGet(90, out _));
            Assert.IsTrue(wealth.TryGet(90, out _));
        }

        [TestCase(1, 1)]
        [TestCase(37, 37)]
        [TestCase(60, 60)]
        [TestCase(0, 10)]
        [TestCase(-1, 10)]
        [TestCase(61, 10)]
        public void ServerValidatesRoomWaitAndOnlyAcceptsWaitingRoomOwner(int requested, int expected)
        {
            var go = new UnityEngine.GameObject("Wait setting test");
            go.SetActive(false);
            var previousTransport = Transport.active;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                Transport.active = go.AddComponent<TelepathyTransport>();
                var network = go.AddComponent<NetworkRoom>();
                var players = (PlayerManager)typeof(NetworkRoom).GetField("_playerManager", flags).GetValue(network);
                var rooms = (RoomManager)typeof(NetworkRoom).GetField("_roomManager", flags).GetValue(network);
                var owner = new NetworkConnectionToClient(191) { isAuthenticated = true };
                var guest = new NetworkConnectionToClient(192) { isAuthenticated = true };
                players.BindAccount(new PlayerAccount { AccountId = "owner", UserName = "Owner" }, owner);
                players.BindAccount(new PlayerAccount { AccountId = "guest", UserName = "Guest" }, guest);
                var room = rooms.CreateRoom("Owner");
                var handler = typeof(NetworkRoom).GetMethod("RoomInfoChangeHandler", flags);
                var request = new RoomInfoChange { RoomId = room.Id, PlayerName = "Owner", SmallBlindChips = 1,
                    ThinkingTime = 30, MaxPlayer = 8, InterHandWaitSeconds = requested };
                handler.Invoke(network, new object[] { guest, request });
                Assert.AreEqual(10, room.InterHandWaitSeconds, "A claimed owner name must not authorize another connection.");
                room.IsWaiting = false;
                handler.Invoke(network, new object[] { owner, request });
                Assert.AreEqual(10, room.InterHandWaitSeconds, "Settings must stay fixed during play.");
                room.IsWaiting = true;
                handler.Invoke(network, new object[] { owner, request });
                Assert.AreEqual(expected, room.InterHandWaitSeconds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                Transport.active = previousTransport;
            }
        }

        [Test]
        public void RoomWaitDefaultsAndConfiguredValueSurviveBothWireFormats()
        {
            Assert.AreEqual(10, new RoomInfo().InterHandWaitSeconds);
            Assert.AreEqual(10, System.Text.Json.JsonSerializer.Deserialize<RoomInfo>("{}").InterHandWaitSeconds);
            using var writer = NetworkWriterPool.Get();
            writer.WriteRoomInfo(new RoomInfo { InterHandWaitSeconds = 37 });
            writer.Write(new RoomInfoChange { InterHandWaitSeconds = 37 });
            var reader = new NetworkReader(writer.ToArraySegment());
            Assert.AreEqual(37, reader.ReadRoomInfo().InterHandWaitSeconds);
            Assert.AreEqual(37, reader.Read<RoomInfoChange>().InterHandWaitSeconds);
        }

        [Test]
        public void FullCollectionAndLeaderboardKindRoundTripThroughMirror()
        {
            using var writer = NetworkWriterPool.Get();
            writer.Write(new CollectionResponse
            {
                RequestId = 12, Revision = 25, AccountId = "owner", Success = true,
                Items = new ItemShopResponse { HasSnapshot = true, Chips = 120,
                    Items = new[] { new ItemStackMessage { ItemId = "item", Quantity = 3 } } },
                Namecards = new NamecardShopResponse { HasSnapshot = true, OwnedIds = new[] { "galaxy" }, EquippedId = "galaxy" },
                CardFaces = new CardFaceShopResponse { HasSnapshot = true, OwnedIds = new[] { "default" }, EquippedId = "default" }
            });
            writer.Write(new LeaderboardRequest { RequestId = 8, Kind = LeaderboardKind.Service });
            writer.Write(new LeaderboardResponse { RequestId = 8, Kind = LeaderboardKind.Service });
            var reader = new NetworkReader(writer.ToArraySegment());
            var collection = reader.Read<CollectionResponse>();
            Assert.IsTrue(collection.Success);
            Assert.AreEqual(25, collection.Revision);
            Assert.AreEqual("owner", collection.AccountId);
            Assert.AreEqual(3, collection.Items.Items[0].Quantity);
            Assert.AreEqual("galaxy", collection.Namecards.EquippedId);
            Assert.AreEqual("default", collection.CardFaces.OwnedIds[0]);
            Assert.AreEqual(LeaderboardKind.Service, reader.Read<LeaderboardRequest>().Kind);
            Assert.AreEqual(LeaderboardKind.Service, reader.Read<LeaderboardResponse>().Kind);
        }
    }
}
