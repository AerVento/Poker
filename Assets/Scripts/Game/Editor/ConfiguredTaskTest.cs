using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Facts;
using Game.Network;
using Game.Network.Accounts;
using Game.Network.Tasks;
using Game.Tasks;
using Microsoft.Data.Sqlite;
using Mirror;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed partial class ConfiguredTaskTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private ConfiguredTaskService _service;
        private DateTimeOffset _now;
        private const string Account = "TaskTest";
        private static string Def(string id, string type, string parameters, int repeats = 1) =>
            "{\"id\":\"" + id + "\",\"name\":\"" + id + "\",\"type\":\"" + type + "\",\"params\":" + parameters + ",\"maxCompletions\":" + repeats + "}";
        private static string Master(params string[] definitions) => "{\"schemaVersion\":2,\"revision\":\"test\",\"tasks\":[" + string.Join(",", definitions) + "]}";
        private static string Fixed(params string[] ids) => JsonSerializer.Serialize(new { schemaVersion = 2, revision = "test", entries = ids.Select(id => new { taskId = id, enabled = true, sortOrder = 0 }).ToArray() });
        private static string Random(int capacity, int days, params string[] ids) => JsonSerializer.Serialize(new { schemaVersion = 2, revision = "test", capacity,
            entries = ids.Select(id => new { taskId = id, enabled = true, weight = 1, validityDays = days }).ToArray() });

        [SetUp]
        public void Setup()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerConfiguredTask-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            _repository.Initialize();
            _repository.Create(new PlayerAccount { AccountId = Account, UserName = Account, Chips = 900,
                PasswordAlgorithm = "test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 1 }, PasswordIterations = 1,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            _now = new DateTimeOffset(2026, 9, 7, 20, 0, 0, TimeSpan.FromHours(8));
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private void Start(string master, string fixedPool, string random)
        {
            var catalog = TaskCatalog.Load(master, fixedPool, random, id => id == "test_item" ? 3u : (uint?)null);
            _service = new ConfiguredTaskService(_repository, catalog, () => _now, new WeightedTaskSelector(() => 0));
            _service.Initialize();
            _service.OnLogin(Account);
        }
        private DailyTaskState State(string id) => _service.GetStatus(Account).Tasks.First(t => t.DefinitionId == id);
        private HandFact Fact(string hand, FormedPattern patterns = FormedPattern.FullHouse, HandFactKind kind = HandFactKind.PatternObserved, bool won = false, int street = 1) =>
            new HandFact { EventId = Account + ":" + hand + ":" + kind + ":" + street, AccountId = Account, HandId = hand, Kind = kind, Street = street, Patterns = patterns, WonAnyPot = won };
        private TaskClaimOutcome Claim(DailyTaskState state) => _service.Claim(Account, state.InstanceId, state.ClaimOrdinal);
        private void Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
        }

        private TaskRewardAccess BeginRewardHand(string handId = "reward-hand")
        {
            if (_repository.FindById("TaskOther") == null)
                _repository.Create(new PlayerAccount { AccountId = "TaskOther", UserName = "TaskOther", Chips = 900,
                    PasswordAlgorithm = "test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 1 },
                    PasswordIterations = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            _repository.SetLastRoom(Account, 7);
            Assert.AreEqual(HandStartResult.Started, _repository.BeginHand(handId, 7, new[] { Account, "TaskOther" }, 1, out _));
            return new TaskRewardAccess(Account, 7, handId);
        }

        [Test]
        public void FoldedRewardIsImmediateAndSettlementKeepsItOutsideHandProfit()
        {
            Start(Master(Def("direct", "DirectClaim", "[100]")), Fixed("direct"), Random(0, 1));
            var state = State("direct");
            var access = BeginRewardHand();
            Assert.AreEqual(DailyTaskResult.ActiveHand, Claim(state).Result);
            Assert.AreEqual(DailyTaskAvailability.ActiveHand, State("direct").Availability);
            Assert.AreEqual(DailyTaskAvailability.Claimable, _service.GetStatus(Account, access: access).Tasks.Single().Availability);
            Assert.AreEqual(DailyTaskResult.Success, _service.Claim(Account, state.InstanceId, 1, access).Result);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
            Assert.AreEqual(DailyTaskResult.AlreadyClaimed, _service.Claim(Account, state.InstanceId, 1, access).Result);
            var entries = new[] {
                new HandSettlementEntry { AccountId = Account, Contribution = 200 },
                new HandSettlementEntry { AccountId = "TaskOther", Contribution = 100, Award = 300 } };
            Assert.AreEqual(HandSettlementResult.Applied, _repository.SettleHand("reward-hand", "reward-settlement", entries, out var balances));
            Assert.AreEqual(700, balances[Account].Chips);
            Assert.AreEqual(800, balances[Account].AccountChips);
            Assert.AreEqual(800, _repository.FindById(Account).Chips);
            Assert.AreEqual(1100, _repository.FindById("TaskOther").Chips);
            Assert.AreEqual(HandSettlementResult.AlreadyApplied, _repository.SettleHand("reward-hand", "reward-settlement", entries, out balances));
            Assert.AreEqual(800, balances[Account].AccountChips);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
            Assert.AreEqual(HandStartResult.Started, _repository.BeginHand("next-reward-hand", 7, new[] { Account, "TaskOther" }, 1, out var accounts));
            Assert.AreEqual(800, accounts.Single(a => a.AccountId == Account).Chips);
        }

        [Test]
        public void TaskAuthorizationCannotCrossAccountsRoomsOrHandsAndBalanceUsesStoredAccount()
        {
            Start(Master(Def("low", "DirectClaimWhenLesserThan", "[100,800]"), Def("direct", "DirectClaim", "[100]")),
                Fixed("low", "direct"), Random(0, 1));
            var low = State("low");
            var state = State("direct");
            var access = BeginRewardHand();
            Assert.AreEqual(DailyTaskResult.BalanceTooHigh, _service.Claim(Account, low.InstanceId, 1, access).Result);
            foreach (var wrong in new[] { new TaskRewardAccess("TaskOther", 7, "reward-hand"),
                new TaskRewardAccess(Account, 8, "reward-hand"), new TaskRewardAccess(Account, 7, "old-hand"), new TaskRewardAccess(Account, 7) })
                Assert.AreEqual(DailyTaskResult.ActiveHand, _service.Claim(Account, state.InstanceId, 1, wrong).Result);
            Assert.AreEqual(900, _repository.FindById(Account).Chips);
        }

        [Test]
        public void DeletingTaskUsesSameRewardGateAndRecoveryPreservesClaimedRewards()
        {
            Start(Master(Def("direct", "DirectClaim", "[100]")), Fixed(), Random(1, 1, "direct"));
            var state = State("direct");
            var access = BeginRewardHand();
            Assert.AreEqual(DailyTaskResult.ActiveHand, _service.Delete(Account, state.InstanceId).Result);
            Assert.AreEqual(DailyTaskResult.Success, _service.Delete(Account, state.InstanceId, access).Result);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
            _repository.Initialize();
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
            Assert.AreEqual(DailyTaskResult.AlreadyDeleted, _service.Delete(Account, state.InstanceId).Result);
            Assert.AreEqual(0, _repository.ReconcileChips().ActiveHandCount);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void UnexpectedBalanceChangesStillRejectSettlement()
        {
            Start(Master(Def("direct", "DirectClaim", "[100]")), Fixed("direct"), Random(0, 1));
            BeginRewardHand();
            Assert.AreEqual(ChipMutationResult.AccountBusyInHand,
                _repository.ApplyChipMutation(Account, "unexpected", "TestAdjustment", 1, null, null, out _));
            Sql("UPDATE player_accounts SET chips=chips+1 WHERE account_id='TaskTest';");
            Assert.AreEqual(HandSettlementResult.InvalidDistribution, _repository.SettleHand("reward-hand", "settle", new[] {
                new HandSettlementEntry { AccountId = Account }, new HandSettlementEntry { AccountId = "TaskOther" } }, out _));
        }

        [Test]
        public void DeleteFreesSlotWithoutRefillingUntilNextBusinessDayEvenAfterRestart()
        {
            Start(Master(Def("fh", "FormPattern", "[100,\"FullHouse\",2]")), Fixed(), Random(1, 3, "fh"));
            _service.Consume(Fact("before"));
            var old = State("fh");
            Assert.IsTrue(old.CanDelete);
            Assert.AreEqual(DailyTaskResult.Success, _service.Delete(Account, old.InstanceId).Result);
            var ignored = Fact("after");
            _service.Consume(ignored);
            _service = new ConfiguredTaskService(new SqliteAccountRepository(_path), _service.Catalog, () => _now);
            _service.Initialize();
            _service.OnLogin(Account);
            Assert.IsEmpty(_service.GetStatus(Account).Tasks);
            Assert.AreEqual(900, _repository.FindById(Account).Chips);
            _now = _now.AddDays(1);
            _service.OnLogin(Account);
            Assert.AreNotEqual(old.InstanceId, State("fh").InstanceId);
            _service.Consume(ignored);
            Assert.AreEqual(0, State("fh").Objectives[0].Progress);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeleteAutoClaimsOnlyPendingRoundIncludingExpiredRewards(bool expired)
        {
            Start(Master(Def("fh", "FormPattern", "[100,\"FullHouse\",1]", 3)), Fixed(), Random(1, 3, "fh"));
            _service.Consume(Fact("h"));
            var state = State("fh");
            if (expired) _now = _now.AddDays(4);
            var outcome = _service.Delete(Account, state.InstanceId);
            Assert.AreEqual(DailyTaskResult.Success, outcome.Result);
            Assert.AreEqual(100, outcome.AutoClaim.Chip);
            Assert.AreEqual(1, outcome.AutoClaim.Ordinal);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
            Assert.IsEmpty(_service.GetStatus(Account).Tasks);
            Assert.AreEqual(DailyTaskResult.AlreadyDeleted, _service.Delete(Account, state.InstanceId).Result);
            Assert.AreEqual(DailyTaskResult.AlreadyClaimed, Claim(state).Result);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
        }

        [TestCase("DirectClaim", "[100]", 100)]
        [TestCase("DirectClaimWhenLesserThan", "[100,900]", 0)]
        public void DeleteDirectTaskClaimsAtMostOneCurrentlyEligibleRound(string type, string args, int reward)
        {
            Start(Master(Def("direct", type, args, 3)), Fixed(), Random(1, 1, "direct"));
            var outcome = _service.Delete(Account, State("direct").InstanceId);
            Assert.AreEqual(DailyTaskResult.Success, outcome.Result);
            Assert.AreEqual(reward, outcome.AutoClaim?.Chip ?? 0);
            Assert.AreEqual(900 + reward, _repository.FindById(Account).Chips);
            Assert.IsEmpty(_service.GetStatus(Account).Tasks);
        }

        [Test]
        public void DeleteRejectsFixedUnknownExpiredAndOtherAccountsInstances()
        {
            Start(Master(Def("fixed", "DirectClaim", "[100]"), Def("random", "PlayHands", "[100,1]")), Fixed("fixed"), Random(1, 1, "random"));
            Assert.IsFalse(State("fixed").CanDelete);
            Assert.AreEqual(DailyTaskResult.FixedTask, _service.Delete(Account, State("fixed").InstanceId).Result);
            Assert.AreEqual(DailyTaskResult.InvalidRequest, _service.Delete(Account, "bad-id").Result);
            Assert.AreEqual(DailyTaskResult.InvalidRequest, _service.Delete(Account, Guid.NewGuid().ToString("N")).Result);
            _repository.Create(new PlayerAccount { AccountId = "Bob", UserName = "Bob", Chips = 900,
                PasswordAlgorithm = "test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 1 }, PasswordIterations = 1,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            var random = State("random");
            _service.OnLogin("Bob");
            Assert.AreEqual(DailyTaskResult.InvalidRequest, _service.Delete("Bob", random.InstanceId).Result);
            _now = _now.AddDays(1);
            Assert.AreEqual(DailyTaskResult.Expired, _service.Delete(Account, random.InstanceId).Result);
            Assert.AreEqual(900, _repository.FindById(Account).Chips);
        }

        [Test]
        public void FailedDeleteAutoClaimRetainsTaskAndRollsBackAllRewards()
        {
            Start(Master(Def("direct", "DirectClaim", "[100]", 3)), Fixed(), Random(1, 1, "direct"));
            var state = State("direct");
            Sql("CREATE TRIGGER fail_task_save BEFORE UPDATE ON task_accounts BEGIN SELECT RAISE(ABORT,'task save failure'); END;");
            Assert.Throws<SqliteException>(() => _service.Delete(Account, state.InstanceId));
            Assert.AreEqual(900, _repository.FindById(Account).Chips);
            Assert.AreEqual(0, State("direct").ClaimedCount);
            Sql("DROP TRIGGER fail_task_save;");
            Assert.AreEqual(DailyTaskResult.Success, _service.Delete(Account, state.InstanceId).Result);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
        }

        [Test]
        public void DeleteCannotDiscardClaimableRewardWhenInventoryIsFull()
        {
            Start(Master(Def("items", "DirectClaim", "[{\"chip\":100,\"items\":[{\"itemId\":\"test_item\",\"count\":2}]}]", 3)), Fixed(), Random(1, 1, "items"));
            Assert.AreEqual(DailyTaskResult.Success, Claim(State("items")).Result);
            Assert.AreEqual(DailyTaskResult.ItemLimit, _service.Delete(Account, State("items").InstanceId).Result);
            Assert.AreEqual(1, State("items").ClaimedCount);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
        }

        [Test]
        public void ConcurrentClaimAndDeleteCannotPaySameCompletedRoundTwice()
        {
            Start(Master(Def("fh", "FormPattern", "[100,\"FullHouse\",1]", 3)), Fixed(), Random(1, 3, "fh"));
            _service.Consume(Fact("h"));
            var state = State("fh");
            var attempts = new[] { System.Threading.Tasks.Task.Run(() => Claim(state).Result),
                System.Threading.Tasks.Task.Run(() => _service.Delete(Account, state.InstanceId).Result) };
            System.Threading.Tasks.Task.WaitAll(attempts);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
            Assert.IsEmpty(_service.GetStatus(Account).Tasks);
        }

        [TestCase("DirectClaim", "[100]", typeof(DirectClaim))]
        [TestCase("DirectClaimWhenLesserThan", "[100,1000]", typeof(DirectClaimWhenLesserThan))]
        [TestCase("DirectClaimWhenAtMost", "[100,1000]", typeof(DirectClaimWhenAtMost))]
        [TestCase("PlayHands", "[100,10]", typeof(PlayHands))]
        [TestCase("WinHands", "[100,10]", typeof(WinHands))]
        [TestCase("FormPattern", "[100,\"TwoPair\",10]", typeof(FormPattern))]
        [TestCase("WinWithPattern", "[100,\"FullHouse\",2]", typeof(WinWithPattern))]
        [TestCase("CountHands", "[100,2,[{\"type\":\"Won\",\"params\":[]}]]", typeof(CountHands))]
        [TestCase("AllObjectives", "[100,[{\"id\":\"plays\",\"type\":\"CountDistinctHands\",\"params\":[10,[{\"type\":\"HandSettled\",\"params\":[]}]]}]]", typeof(AllObjectives))]
        public void EveryTaskTypeCompilesFromParameters(string type, string args, Type expected) =>
            Assert.That(TaskFactory.Compile(Def("task", type, args)).Rule, Is.TypeOf(expected));

        [Test]
        public void StartupRejectsCrossPoolReferencesInvalidTypesAndMultiDayDirectClaims()
        {
            var master = Master(Def("direct", "DirectClaim", "[100]"));
            Assert.Throws<ArgumentException>(() => TaskCatalog.Load(master, Fixed("direct"), Random(1, 1, "direct")));
            Assert.Throws<ArgumentException>(() => TaskCatalog.Load(master, Fixed(), Random(1, 3, "direct")));
            Assert.Throws<ArgumentException>(() => TaskFactory.Compile(Def("task", "HighCard", "[100]")));
            Assert.Throws<ArgumentException>(() => TaskFactory.Compile(Def("task", "FormPattern", "[100,\"HighCard\",1]")));
            Assert.Throws<ArgumentException>(() => TaskFactory.Compile(Def("task", "PlayHands", "[100,0]")));
            Assert.Throws<ArgumentException>(() => TaskFactory.Compile(Def("task", "DirectClaim", "[100,10]")));
            Assert.Throws<ArgumentException>(() => TaskCatalog.Load(master, Fixed("unknown"), Random(0, 1)));
        }

        [Test]
        public void WeightedSelectionIsRelativeWithoutReplacementAndExcludesDisabledOrZero()
        {
            var entries = new[] { new TaskPoolEntry { TaskId = "a", Weight = 1 }, new TaskPoolEntry { TaskId = "b", Weight = 3 },
                new TaskPoolEntry { TaskId = "zero", Weight = 0 }, new TaskPoolEntry { TaskId = "disabled", Weight = 100, Enabled = false } };
            Assert.AreEqual("b", new WeightedTaskSelector(() => 0.3).Select(entries, 1)[0].TaskId);
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, new WeightedTaskSelector(() => 0.99).Select(entries, 10).Select(e => e.TaskId));
        }

        [Test]
        public void RepeatRequiresClaimAndNeverReusesSameHandOrQueuedEvents()
        {
            Start(Master(Def("fh", "FormPattern", "[100,\"FullHouse\",1]", 3)), Fixed(), Random(1, 3, "fh"));
            var first = Fact("h1");
            _service.Consume(first);
            Assert.AreEqual(1, State("fh").CompletedCount);
            _service.Consume(Fact("ignored"));
            Assert.AreEqual(DailyTaskResult.Success, Claim(State("fh")).Result);
            Assert.AreEqual(0, State("fh").Objectives[0].Progress);
            _service.Consume(first);
            _service.Consume(Fact("ignored"));
            _service.Consume(Fact("h1", street: 2));
            Assert.AreEqual(0, State("fh").Objectives[0].Progress);
            _service.Consume(Fact("h2"));
            Assert.AreEqual(2, State("fh").CompletedCount);
            Assert.AreEqual(DailyTaskResult.Success, Claim(State("fh")).Result);
            _service.Consume(Fact("h3"));
            Assert.AreEqual(DailyTaskResult.Success, Claim(State("fh")).Result);
            Assert.AreEqual(1200, _repository.FindById(Account).Chips);
            Assert.AreEqual(3, State("fh").ClaimedCount);
            Assert.AreEqual(1, State("fh").Objectives[0].Progress);
        }

        [Test]
        public void OneHandCanAdvanceSeveralPatternsButWinningObjectivesWaitForSettlement()
        {
            Start(Master(Def("fh", "FormPattern", "[100,\"FullHouse\",1]"), Def("quad", "FormPattern", "[100,\"FourOfAKind\",1]"),
                Def("win", "WinWithPattern", "[100,\"FullHouse\",1]")), Fixed(), Random(3, 3, "fh", "quad", "win"));
            _service.Consume(Fact("h", FormedPattern.FullHouse));
            _service.Consume(Fact("h", FormedPattern.FullHouse | FormedPattern.FourOfAKind, street: 2));
            Assert.AreEqual(1, State("fh").CompletedCount);
            Assert.AreEqual(1, State("quad").CompletedCount);
            Assert.AreEqual(0, State("win").CompletedCount);
            _service.Consume(Fact("loss", FormedPattern.FullHouse, HandFactKind.Settled, false));
            Assert.AreEqual(0, State("win").CompletedCount);
            _service.Consume(Fact("h", FormedPattern.FullHouse | FormedPattern.FourOfAKind, HandFactKind.Settled, true));
            Assert.AreEqual(1, State("win").CompletedCount);
        }

        [Test]
        public void ExpiryUsesProcessingTimeAndPendingRewardSurvivesWithoutOpeningAnotherRound()
        {
            Start(Master(Def("fh", "FormPattern", "[100,\"FullHouse\",1]", 3)), Fixed(), Random(1, 3, "fh"));
            var instance = State("fh");
            Assert.AreEqual(new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.FromHours(8)).ToUnixTimeSeconds(), instance.ExpiresAtUnixSeconds);
            _now = DateTimeOffset.FromUnixTimeSeconds(instance.ExpiresAtUnixSeconds).AddMilliseconds(-1);
            _service.Consume(Fact("before"));
            _now = _now.AddDays(10);
            Assert.AreEqual(DailyTaskAvailability.Claimable, State("fh").Availability);
            Assert.AreEqual(DailyTaskResult.Success, Claim(State("fh")).Result);
            _service.Consume(Fact("late"));
            Assert.That(_service.GetStatus(Account).Tasks, Is.Empty);
        }

        [Test]
        public void FactAtExactDeadlineCannotCompleteTaskAndReplayCannotTargetNewInstance()
        {
            Start(Master(Def("fh", "FormPattern", "[100,\"FullHouse\",1]")), Fixed(), Random(1, 1, "fh"));
            var old = State("fh");
            _now = DateTimeOffset.FromUnixTimeSeconds(old.ExpiresAtUnixSeconds);
            var late = Fact("late");
            _service.Consume(late);
            _service.OnLogin(Account);
            var fresh = State("fh");
            Assert.AreNotEqual(old.InstanceId, fresh.InstanceId);
            _service.Consume(late);
            Assert.AreEqual(0, State("fh").CompletedCount);
        }

        [Test]
        public void CurrentWinningFactCanUsePatternHistoryFromBeforeAssignment()
        {
            Start(Master(Def("win", "WinWithPattern", "[100,\"FullHouse\",1]")), Fixed(), Random(1, 1, "win"));
            _service.Consume(Fact("cross"));
            _now = _now.AddDays(1);
            _service.OnLogin(Account);
            _service.Consume(Fact("cross", FormedPattern.FullHouse, HandFactKind.Settled, true));
            Assert.AreEqual(1, State("win").CompletedCount);
        }

        [Test]
        public void FixedDailyRefreshAndRandomFirstLoginOnlyRespectRemainingRounds()
        {
            Start(Master(Def("daily", "DirectClaim", "[10]", 3), Def("fh", "FormPattern", "[100,\"FullHouse\",1]", 3)), Fixed("daily"), Random(1, 3, "fh"));
            var fixedId = State("daily").InstanceId;
            var randomId = State("fh").InstanceId;
            _service.Consume(Fact("h"));
            _now = _now.AddDays(1);
            _service.RefreshAllFixed();
            _service.OnLogin(Account);
            Assert.AreNotEqual(fixedId, State("daily").InstanceId);
            Assert.AreEqual(randomId, State("fh").InstanceId);
            Assert.AreEqual(2, _service.GetStatus(Account).Tasks.Length);
            Claim(State("fh")); _service.Consume(Fact("h2")); Claim(State("fh")); _service.Consume(Fact("h3"));
            _service.OnLogin(Account);
            Assert.AreEqual(2, _service.GetStatus(Account).Tasks.Length, "Same-day login must not refill a freed final-round slot.");
            _now = _now.AddDays(1); _service.OnLogin(Account);
            Assert.AreEqual(1, _service.GetStatus(Account).Tasks.Count(t => t.DefinitionId == "fh"), "Completed tasks are auto-claimed and replaced on the next business day.");
            Assert.AreEqual(1200, _repository.FindById(Account).Chips);
        }

        [Test]
        public void DirectClaimRechecksBalanceEachRoundAndCannotBeBackClaimed()
        {
            Start(Master(Def("aid", "DirectClaimWhenLesserThan", "[100,1000]", 3)), Fixed("aid"), Random(0, 1));
            var first = State("aid");
            Assert.AreEqual(DailyTaskResult.Success, Claim(first).Result);
            Assert.AreEqual(DailyTaskResult.AlreadyClaimed, Claim(first).Result);
            Assert.AreEqual(DailyTaskResult.BalanceTooHigh, Claim(State("aid")).Result);
            _now = _now.AddDays(1);
            Assert.AreEqual(DailyTaskResult.Expired, _service.Claim(Account, first.InstanceId, 2).Result);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
        }

        [Test]
        public void ConcurrentClaimAttemptsGrantOnlyOnce()
        {
            Start(Master(Def("daily", "DirectClaim", "[100]", 3)), Fixed("daily"), Random(0, 1));
            var state = State("daily");
            var attempts = Enumerable.Range(0, 6).Select(_ => System.Threading.Tasks.Task.Run(() => Claim(state).Result)).ToArray();
            System.Threading.Tasks.Task.WaitAll(attempts);
            Assert.AreEqual(1, attempts.Count(t => t.Result == DailyTaskResult.Success));
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
            Assert.AreEqual(1, State("daily").ClaimedCount);
        }

        [Test]
        public void GrantAndReceiptRollbackTogetherOnStorageFailure()
        {
            Start(Master(Def("daily", "DirectClaim", "[100]", 3)), Fixed("daily"), Random(0, 1));
            var state = State("daily");
            Sql("CREATE TRIGGER fail_task_save BEFORE UPDATE ON task_accounts BEGIN SELECT RAISE(ABORT,'task save failure'); END;");
            Assert.Throws<SqliteException>(() => Claim(state));
            Assert.AreEqual(900, _repository.FindById(Account).Chips);
            Assert.AreEqual(0, State("daily").ClaimedCount);
            Sql("DROP TRIGGER fail_task_save;");
            Assert.AreEqual(DailyTaskResult.Success, Claim(state).Result);
        }

        [Test]
        public void ItemRewardsMergeAndInventoryLimitDoesNotConsumeRoundOrChips()
        {
            Start(Master(Def("items", "DirectClaim", "[{\"chip\":100,\"items\":[{\"itemId\":\"test_item\",\"count\":1},{\"itemId\":\"test_item\",\"count\":1}]}]", 3)), Fixed("items"), Random(0, 1));
            Assert.AreEqual(2, Claim(State("items")).Receipt.Items.Single().Count);
            Assert.AreEqual(DailyTaskResult.ItemLimit, Claim(State("items")).Result);
            Assert.AreEqual(1, State("items").ClaimedCount);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
            var a = new Reward(1, new[] { new ItemStack("test_item", 1) });
            var b = a + a;
            Assert.AreEqual(2, b.Items.Single().Quantity);
            Assert.AreEqual(1, a.Items.Single().Quantity);
            Assert.Throws<OverflowException>(() => { var ignored = new Reward(uint.MaxValue) + a; });
        }

        [Test]
        public void OutboxAndInboxSurviveRestartAndProgressIsNeverReverted()
        {
            Start(Master(Def("fh", "FormPattern", "[100,\"FullHouse\",2]")), Fixed(), Random(1, 3, "fh"));
            var fact = Fact("h");
            _repository.PublishHandFact(fact); _repository.PublishHandFact(fact);
            Assert.AreEqual(1, _repository.ReadPendingHandFacts(100).Length);
            var restarted = new ConfiguredTaskService(new SqliteAccountRepository(_path), _service.Catalog, () => _now);
            restarted.DrainFacts();
            Assert.AreEqual(1, State("fh").Objectives[0].Progress);
            Assert.AreEqual(0, _repository.ReadPendingHandFacts(100).Length);
            restarted.Consume(fact);
            Assert.AreEqual(1, State("fh").Objectives[0].Progress);
            fact.Patterns = FormedPattern.FourOfAKind;
            Assert.Throws<InvalidOperationException>(() => _repository.PublishHandFact(fact));
        }

        [Test]
        public void AllObjectivesCountIndependentlyWithoutCountingAnyHandTwice()
        {
            var rule = new AllObjectives(new Reward(100), new ObjectivePlan("play", 2, new HandSettledCondition()),
                new ObjectivePlan("pattern", 1, new PatternObservedCondition(FormedPattern.FullHouse)));
            var state = new TaskState { Progress = new int[2] };
            rule.Evaluate(Fact("h1"), state);
            rule.Evaluate(Fact("h1", kind: HandFactKind.Settled), state);
            rule.Evaluate(Fact("h1", kind: HandFactKind.Settled, street: 2), state);
            CollectionAssert.AreEqual(new[] { 1, 1 }, state.Progress);
            Assert.AreEqual(0, state.CompletedCount);
            rule.Evaluate(Fact("h2", kind: HandFactKind.Settled), state);
            Assert.AreEqual(1, state.CompletedCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TablePublishesOnRevealStopsAtFoldAndAbortDoesNotUndoProgress(bool fold)
        {
            Start(Master(Def("fh", "FormPattern", "[100,\"FullHouse\",1]"), Def("quad", "FormPattern", "[100,\"FourOfAKind\",1]")),
                Fixed(), Random(2, 3, "fh", "quad"));
            var room = new RoomInfo { Id = 1256, MaxPlayerCount = 3, SmallBlindChips = 10, ThinkingTime = 1000 };
            foreach (var name in new[] { Account, "Bob", "Carol" })
            {
                if (name != Account) _repository.Create(new PlayerAccount { AccountId = name, UserName = name, Chips = 900,
                    PasswordAlgorithm = "test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 1 }, PasswordIterations = 1,
                    CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
                room.Players.Add(name); room.ReadyStatus.Add(true);
            }
            var manager = new GameManager(_repository);
            Assert.AreEqual(0, manager.CreateGame(room));
            Assert.AreEqual(0, manager.StartNew(room.Id));
            var info = manager.GetGameInfo(room.Id);
            info.Players[0].Hole = new System.Collections.Generic.List<Poker> { new Poker(PokerSuit.Heart, PokerRank.Ace), new Poker(PokerSuit.Diamond, PokerRank.Ace) };
            info.Players[1].Hole = new System.Collections.Generic.List<Poker> { new Poker(PokerSuit.Heart, PokerRank.Queen), new Poker(PokerSuit.Diamond, PokerRank.Queen) };
            info.Players[2].Hole = new System.Collections.Generic.List<Poker> { new Poker(PokerSuit.Heart, PokerRank.Jack), new Poker(PokerSuit.Diamond, PokerRank.Jack) };
            info.Deck = new System.Collections.Generic.Queue<Poker>(new[]
            {
                new Poker(PokerSuit.Spade, PokerRank.Two), new Poker(PokerSuit.Club, PokerRank.Ace),
                new Poker(PokerSuit.Heart, PokerRank.King), new Poker(PokerSuit.Diamond, PokerRank.King),
                new Poker(PokerSuit.Spade, PokerRank.Three), new Poker(PokerSuit.Spade, PokerRank.Ace)
            });
            Assert.AreEqual(0, manager.NextStage(room.Id));
            Assert.AreEqual(GameStage.Flop, info.Stage);
            _service.DrainFacts();
            Assert.AreEqual(1, State("fh").CompletedCount, "Flop reveal must publish before flop betting finishes.");
            info.Players[0].IsFold = fold;
            Assert.AreEqual(0, manager.NextStage(room.Id));
            _service.DrainFacts();
            Assert.AreEqual(fold ? 0 : 1, State("quad").CompletedCount);
            _repository.AbortHand(info.HandId, "test cancellation");
            _service.DrainFacts();
            Assert.AreEqual(1, State("fh").CompletedCount);
        }

        [Test]
        public void SettlementAndWinningOutboxCommitInSameTransaction()
        {
            Start(Master(Def("win", "WinHands", "[100,1]")), Fixed(), Random(1, 3, "win"));
            _repository.Create(new PlayerAccount { AccountId = "Bob", UserName = "Bob", Chips = 900,
                PasswordAlgorithm = "test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 1 }, PasswordIterations = 1,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            Assert.AreEqual(HandStartResult.Started, _repository.BeginHand("atomic", 555, new[] { Account, "Bob" }, 1, out _));
            var fact = Fact("atomic", FormedPattern.None, HandFactKind.Settled, true);
            var entries = new[] { new HandSettlementEntry { AccountId = Account, OutcomeFact = fact }, new HandSettlementEntry { AccountId = "Bob" } };
            Sql("CREATE TRIGGER fail_fact BEFORE INSERT ON game_facts BEGIN SELECT RAISE(ABORT,'outbox failure'); END;");
            Assert.Throws<SqliteException>(() => _repository.SettleHand("atomic", "atomic:settle", entries, out _));
            Assert.That(_repository.ReadPendingHandFacts(100), Is.Empty);
            Sql("DROP TRIGGER fail_fact;");
            Assert.AreEqual(HandSettlementResult.Applied, _repository.SettleHand("atomic", "atomic:settle", entries, out _));
            Assert.AreEqual(1, _repository.ReadPendingHandFacts(100).Length);
            _service.DrainFacts();
            Assert.AreEqual(1, State("win").CompletedCount);
        }

        [Test]
        public void PaginationDetectsChangedRevisionAndNetworkCarriesAllState()
        {
            var ids = Enumerable.Range(0, 45).Select(i => "task" + i).ToArray();
            Start(Master(ids.Select(id => Def(id, "DirectClaim", "[100]", 3)).ToArray()), Fixed(ids), Random(0, 1));
            var page = _service.GetStatus(Account);
            Assert.AreEqual(40, page.Tasks.Length);
            var last = _service.GetStatus(Account, page.NextOffset, page.Revision);
            Assert.AreEqual(5, last.Tasks.Length);
            Assert.AreEqual(-1, last.NextOffset);
            Claim(page.Tasks[0]);
            Assert.IsNull(_service.GetStatus(Account, page.NextOffset, page.Revision).Tasks);
            using var writer = NetworkWriterPool.Get();
            writer.Write(new DailyTasksResponse { Result = DailyTaskResult.Success, Snapshot = page });
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            var restored = reader.Read<DailyTasksResponse>().Snapshot;
            Assert.AreEqual(page.Tasks[0].InstanceId, restored.Tasks[0].InstanceId);
            Assert.AreEqual(3, restored.Tasks[0].MaxCompletions);
            Assert.AreEqual(page.Revision, restored.Revision);
            Assert.AreEqual(page.Tasks[0].ExpiresAtUnixSeconds, restored.Tasks[0].ExpiresAtUnixSeconds);
        }
    }

    public sealed class FormablePatternTest
    {
        private static Poker C(PokerRank rank, PokerSuit suit) => new Poker(suit, rank);
        [Test]
        public void PocketPairCountsAndFullHouseIncludesPairTwoPairAndTriple()
        {
            var cards = new[] { C(PokerRank.Ace, PokerSuit.Heart), C(PokerRank.Ace, PokerSuit.Diamond), C(PokerRank.Ace, PokerSuit.Club),
                C(PokerRank.King, PokerSuit.Heart), C(PokerRank.King, PokerSuit.Diamond) };
            Assert.AreEqual(FormedPattern.Pair, FormablePatternEvaluator.Evaluate(cards.Take(2)));
            Assert.AreEqual(FormedPattern.Pair | FormedPattern.TwoPair | FormedPattern.ThreeOfAKind | FormedPattern.FullHouse, FormablePatternEvaluator.Evaluate(cards));
            var quad = cards.Take(3).Concat(new[] { C(PokerRank.Ace, PokerSuit.Spade), cards[3] });
            Assert.AreEqual(FormedPattern.Pair | FormedPattern.ThreeOfAKind | FormedPattern.FourOfAKind, FormablePatternEvaluator.Evaluate(quad));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void StraightFlushIncludesBothStraightAndFlushIncludingWheel(bool wheel)
        {
            var ranks = wheel ? new[] { PokerRank.Ace, PokerRank.Two, PokerRank.Three, PokerRank.Four, PokerRank.Five }
                : new[] { PokerRank.Ten, PokerRank.Jack, PokerRank.Queen, PokerRank.King, PokerRank.Ace };
            Assert.AreEqual(FormedPattern.StraightFlush | FormedPattern.Flush | FormedPattern.Straight,
                FormablePatternEvaluator.Evaluate(ranks.Select(r => C(r, PokerSuit.Heart))));
        }
        [Test]
        public void SevenCardsFindSecondaryPatternAndBoardOnlyPattern()
        {
            var board = new[] { C(PokerRank.King, PokerSuit.Heart), C(PokerRank.King, PokerSuit.Diamond), C(PokerRank.King, PokerSuit.Club),
                C(PokerRank.Two, PokerSuit.Heart), C(PokerRank.Two, PokerSuit.Diamond) };
            var flags = FormablePatternEvaluator.Evaluate(board.Concat(new[] { C(PokerRank.King, PokerSuit.Spade), C(PokerRank.Three, PokerSuit.Spade) }));
            Assert.That(flags.HasFlag(FormedPattern.FullHouse));
            Assert.That(flags.HasFlag(FormedPattern.FourOfAKind));
            Assert.That(flags.HasFlag(FormedPattern.TwoPair));
        }
    }
}
