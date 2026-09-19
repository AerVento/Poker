using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Game.Network;
using Game.Network.Accounts;
using Game.UI.Gifts;
using Microsoft.Data.Sqlite;
using Mirror;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GiftUseTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private PlayerAccount _account;
        private ItemPurchaseRuntime _runtime;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerGifts-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            var service = new AccountService(_repository, new PasswordHasher(iterations: 1000));
            service.Initialize();
            _account = service.Register("GiftUser", "password123").Account;
            _runtime = ContractPurchaseRegistration.CreateRuntime(Game.SO.ItemShopCatalogSO.LoadDefault().CreateServerCatalog());
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private void BuyEggs(int quantity)
        {
            for (var index = 0; index < quantity; index++)
            {
                var result = _repository.ChangeItem(_account.AccountId, Guid.NewGuid().ToString("N"),
                    ItemShopAction.Purchase, GiftItemIds.RottenEgg, 1,
                    _runtime.Find(GiftItemIds.RottenEgg).Price, _runtime.Revision, _runtime);
                Assert.AreEqual(ItemShopResult.Success, result.Result);
            }
        }

        private GiftUseExecution Use(string operationId, params GiftUseCommand[] commands)
            => _repository.ConsumeGiftBatch(_account.AccountId, operationId, commands, _runtime);

        private long Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        [Test]
        public void RottenEggSupportsOneAtomicBulkPurchase()
        {
            var result = _repository.ChangeItem(_account.AccountId, Guid.NewGuid().ToString("N"),
                ItemShopAction.Purchase, GiftItemIds.RottenEgg, 100,
                _runtime.Find(GiftItemIds.RottenEgg).Price, _runtime.Revision, _runtime);
            Assert.AreEqual(ItemShopResult.Success, result.Result);
            Assert.AreEqual(5000, result.Outcome.ChargedPrice);
            Assert.AreEqual(5000, result.Outcome.BalanceAfter);
            Assert.AreEqual(100, _repository.ReadItems(_account.AccountId).Items.Single().Quantity);
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='ItemPurchase';"));
        }

        [Test]
        public void RottenEggVisualsUseHalfWidthAndHeight()
        {
            var effect = AssetDatabase.LoadAssetAtPath<RottenEggGiftEffect>(
                "Assets/Prefabs/Gifts/RottenEggGiftEffect.prefab");
            Assert.NotNull(effect);
            Assert.AreEqual(0.5f, effect.VisualScale, 0.0001f);
        }

        [Test]
        public void PolicySeparatesContractsAndStartedRoomGifts()
        {
            Assert.AreEqual(0.2f, GiftInteractionController.PlaybackInterval(2f), 0.0001f);
            var egg = _runtime.Find(GiftItemIds.RottenEgg);
            var eggContext = new ItemUseContext(_account.AccountId, _account.Chips, egg, 1);
            Assert.AreEqual(ItemUseKind.Gift, egg.UsePolicy.Kind);
            Assert.AreEqual(ItemShopResult.RoomStartedRequired,
                ItemPurchaseRuntime.CheckUse(eggContext, ItemUseLocation.OutsideRoom));
            Assert.AreEqual(ItemShopResult.RoomStartedRequired,
                ItemPurchaseRuntime.CheckUse(eggContext, ItemUseLocation.WaitingRoom));
            Assert.AreEqual(ItemShopResult.Success,
                ItemPurchaseRuntime.CheckUse(eggContext, ItemUseLocation.StartedRoom));

            var contract = _runtime.Find("devil-contract");
            Assert.AreEqual(ItemShopResult.OutsideRoomRequired, ItemPurchaseRuntime.CheckUse(
                new ItemUseContext(_account.AccountId, 1100, contract, 1), ItemUseLocation.StartedRoom));
        }

        [Test]
        public void BatchConsumptionIsAtomicIdempotentAndDoesNotTouchChipsOrAccountVersion()
        {
            BuyEggs(5);
            var before = _repository.FindById(_account.AccountId);
            var ledgerCount = Sql("SELECT COUNT(*) FROM chip_ledger;");
            var operation = Guid.NewGuid().ToString("N");
            var commands = new[]
            {
                new GiftUseCommand(GiftItemIds.RottenEgg, 1, 2),
                new GiftUseCommand(GiftItemIds.RottenEgg, 4, 3),
            };

            var first = Use(operation, commands);
            var replay = Use(operation, commands);

            Assert.AreEqual(GiftUseResult.Success, first.Result);
            Assert.IsTrue(first.IsNewlyApplied);
            Assert.AreEqual(GiftUseResult.Success, replay.Result);
            Assert.IsFalse(replay.IsNewlyApplied);
            Assert.IsEmpty(_repository.ReadItems(_account.AccountId).Items);
            var after = _repository.FindById(_account.AccountId);
            Assert.AreEqual(before.Chips, after.Chips);
            Assert.AreEqual(before.Version, after.Version);
            Assert.AreEqual(ledgerCount, Sql("SELECT COUNT(*) FROM chip_ledger;"));
        }

        [Test]
        public void BatchUsesStockThenAffordablePrefixAndRejectsChangedRetry()
        {
            BuyEggs(3);
            Sql("UPDATE player_accounts SET dollars=75; SELECT 1;");
            var before = _repository.FindById(_account.AccountId);
            var operation = Guid.NewGuid().ToString("N");
            var commands = new[]
            {
                new GiftUseCommand(GiftItemIds.RottenEgg, 1, 2),
                new GiftUseCommand(GiftItemIds.RottenEgg, 4, 3),
                new GiftUseCommand(GiftItemIds.RottenEgg, 2, 1),
            };
            var applied = Use(operation, commands);
            Assert.AreEqual(GiftUseResult.Success, applied.Result);
            CollectionAssert.AreEqual(new[] { 1, 4 }, applied.AppliedCommands.Select(command => command.TargetSeatIndex));
            CollectionAssert.AreEqual(new uint[] { 2, 2 }, applied.AppliedCommands.Select(command => command.Quantity));
            Assert.IsEmpty(_repository.ReadItems(_account.AccountId).Items);
            var after = _repository.FindById(_account.AccountId);
            Assert.AreEqual(25, after.Dollars);
            Assert.AreEqual(before.Chips, after.Chips);
            Assert.AreEqual(before.Version + 1, after.Version);
            Assert.IsFalse(Use(operation, commands).IsNewlyApplied);
            Assert.AreEqual(25, _repository.FindById(_account.AccountId).Dollars);
            Assert.AreEqual(GiftUseResult.InvalidRequest, Use(operation,
                new GiftUseCommand(GiftItemIds.RottenEgg, 2, 3)).Result);
        }

        [TestCase(99, true)]
        [TestCase(100, false)]
        [TestCase(500, false)]
        public void GiftRestrictionUsesForcedNamecardThreshold(int years, bool allowed)
        {
            BuyEggs(2);
            Sql("INSERT INTO account_items(account_id,item_id,quantity) SELECT account_id,'devil-contract'," + years + " FROM player_accounts; SELECT 1;");
            var before = _repository.FindById(_account.AccountId);
            var result = Use(Guid.NewGuid().ToString("N"), new GiftUseCommand(GiftItemIds.RottenEgg, 1, 1));
            Assert.AreEqual(allowed ? GiftUseResult.Success : GiftUseResult.ServiceRestricted, result.Result);
            Assert.AreEqual(allowed ? 1 : 2, _repository.ReadItems(_account.AccountId).Items.Single(item => item.ItemId == GiftItemIds.RottenEgg).Quantity);
            Assert.AreEqual(before.Dollars, _repository.FindById(_account.AccountId).Dollars);
        }

        [Test]
        public void MirrorRoundTripsBatchedTargetsAndPlaybackSchedule()
        {
            var request = new GiftUseBatchRequest
            {
                RequestId = 9,
                OperationId = Guid.NewGuid().ToString("N"),
                Commands = new[]
                {
                    new GiftUseCommandMessage { ItemId = GiftItemIds.RottenEgg, TargetSeatIndex = 4, Quantity = 5 },
                },
            };
            using var writer = NetworkWriterPool.Get();
            writer.Write(request);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            var copy = reader.Read<GiftUseBatchRequest>();
            Assert.AreEqual(9, copy.RequestId);
            Assert.AreEqual(4, copy.Commands.Single().TargetSeatIndex);
            Assert.AreEqual(5, copy.Commands.Single().Quantity);

            writer.Reset();
            writer.Write(new GiftPlaybackBatchMessage
            {
                EventId = request.OperationId,
                StartAtNetworkTime = 123.5,
                Commands = new[]
                {
                    new GiftPlaybackCommandMessage
                    {
                        ItemId = GiftItemIds.RottenEgg,
                        SenderSeatIndex = 1,
                        TargetSeatIndex = 4,
                        Quantity = 5,
                    },
                },
            });
            using var playbackReader = NetworkReaderPool.Get(writer.ToArraySegment());
            var playback = playbackReader.Read<GiftPlaybackBatchMessage>();
            Assert.AreEqual(123.5, playback.StartAtNetworkTime);
            Assert.AreEqual(1, playback.Commands.Single().SenderSeatIndex);
            Assert.AreEqual(4, playback.Commands.Single().TargetSeatIndex);
            Assert.AreEqual(5, playback.Commands.Single().Quantity);
        }

        [Test]
        public void ServerDerivesSenderValidatesRoomAndBroadcastsOnlyANewAtomicUse()
        {
            BuyEggs(2);
            var go = new GameObject("Gift request test");
            go.SetActive(false);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var network = go.AddComponent<NetworkRoom>();
                var auth = go.AddComponent<AccountAuthenticator>();
                network.authenticator = auth;
                typeof(AccountAuthenticator).GetField("<AccountRepository>k__BackingField", flags)
                    .SetValue(auth, _repository);
                typeof(NetworkRoom).GetField("_itemRuntime", flags).SetValue(network, _runtime);
                var players = (PlayerManager)typeof(NetworkRoom).GetField("_playerManager", flags).GetValue(network);
                var rooms = (RoomManager)typeof(NetworkRoom).GetField("_roomManager", flags).GetValue(network);
                var target = new AccountService(_repository, new PasswordHasher(iterations: 1000))
                    .Register("GiftTarget", "password123").Account;
                var senderConnection = new NetworkConnectionToClient(81) { isAuthenticated = true };
                var targetConnection = new NetworkConnectionToClient(82) { isAuthenticated = true };
                players.BindAccount(_account, senderConnection);
                players.BindAccount(target, targetConnection);
                var room = rooms.CreateRoom(_account.UserName);
                rooms.Join(room.Id, target.UserName);
                room.IsWaiting = false;
                var method = typeof(NetworkRoom).GetMethod("ProcessGiftUseBatchRequest", flags);
                var operation = Guid.NewGuid().ToString("N");
                GiftUseBatchResponse Send(ulong requestId, int targetSeat, string operationId = null)
                {
                    var args = new object[]
                    {
                        senderConnection,
                        new GiftUseBatchRequest
                        {
                            RequestId = requestId,
                            OperationId = operationId ?? Guid.NewGuid().ToString("N"),
                            Commands = new[]
                            {
                                new GiftUseCommandMessage
                                {
                                    ItemId = GiftItemIds.RottenEgg,
                                    TargetSeatIndex = targetSeat,
                                    Quantity = 2,
                                },
                            },
                        },
                        null,
                        null,
                    };
                    var response = (GiftUseBatchResponse)method.Invoke(network, args);
                    var rates = (System.Collections.Generic.Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)>)
                        typeof(NetworkRoom).GetField("_giftUseRequests", flags).GetValue(network);
                    rates[senderConnection] = (requestId, 0d);
                    if (response.Result == GiftUseResult.Success && response.IsNewlyApplied)
                    {
                        var playback = (GiftPlaybackBatchMessage)args[2];
                        Assert.AreEqual(0, playback.Commands.Single().SenderSeatIndex);
                        Assert.AreEqual(targetSeat, playback.Commands.Single().TargetSeatIndex);
                    }
                    return response;
                }

                Assert.AreEqual(GiftUseResult.CannotTargetSelf, Send(1, 0).Result);
                var first = Send(2, 1, operation);
                var replay = Send(3, 1, operation);
                Assert.AreEqual(GiftUseResult.Success, first.Result);
                Assert.IsTrue(first.IsNewlyApplied);
                Assert.AreEqual(GiftUseResult.Success, replay.Result);
                Assert.IsFalse(replay.IsNewlyApplied);
                Assert.IsEmpty(_repository.ReadItems(_account.AccountId).Items);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
