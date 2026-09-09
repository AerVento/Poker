using System;
using System.Linq;
using Game.Network;
using Game.Network.Accounts;
using Game.Network.Tasks;
using Mirror;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed partial class ConfiguredTaskTest
    {
        [Test]
        public void RefreshUsesSameSlotAllowsOtherSlotsDefinitionAndCannotRepeatAcrossRestart()
        {
            Start(Master(Def("a", "FormPattern", "[100,\"FullHouse\",2]"), Def("b", "FormPattern", "[200,\"FullHouse\",2]")),
                Fixed(), Random(2, 3, "a", "b"));
            var old = State("a");
            _service.Consume(Fact("progress"));
            Assert.IsTrue(old.CanRefresh);
            _now = _now.AddHours(6);
            Assert.AreEqual(DailyTaskResult.Success, _service.Reroll(Account, old.InstanceId).Result);
            var replacement = _service.GetStatus(Account).Tasks.Single(t => t.SlotId == old.SlotId);
            Assert.AreEqual("b", replacement.DefinitionId);
            Assert.AreEqual(2, _service.GetStatus(Account).Tasks.Count(t => t.DefinitionId == "b"));
            Assert.AreEqual(0, replacement.Objectives[0].Progress);
            Assert.IsFalse(replacement.CanRefresh);
            Assert.AreEqual(new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.FromHours(8)).ToUnixTimeSeconds(), replacement.ExpiresAtUnixSeconds);
            Assert.AreEqual(DailyTaskResult.RefreshUnavailable, _service.Reroll(Account, old.InstanceId).Result);
            _service = new ConfiguredTaskService(_repository, _service.Catalog, () => _now);
            _service.OnLogin(Account);
            Assert.AreEqual(DailyTaskResult.RefreshUnavailable, _service.Reroll(Account, replacement.InstanceId).Result);
            Assert.AreEqual(900, _repository.FindById(Account).Chips);
        }

        [Test]
        public void ClaimingAnyRoundPermanentlyRemovesRefreshEligibility()
        {
            Start(Master(Def("a", "DirectClaim", "[10]", 3), Def("b", "DirectClaim", "[10]")), Fixed(), Random(1, 1, "a", "b"));
            var old = State("a");
            Assert.IsTrue(old.CanRefresh);
            Assert.AreEqual(DailyTaskResult.Success, Claim(old).Result);
            Assert.IsFalse(State("a").CanRefresh);
            Assert.AreEqual(DailyTaskResult.RefreshUnavailable, _service.Reroll(Account, old.InstanceId).Result);
            Assert.AreEqual(1, State("a").ClaimedCount);
        }

        [Test]
        public void NoReplacementRetainsProgressAndFreeRefresh()
        {
            Start(Master(Def("a", "FormPattern", "[100,\"FullHouse\",2]")), Fixed(), Random(1, 3, "a"));
            _service.Consume(Fact("progress"));
            var old = State("a");
            Assert.AreEqual(DailyTaskResult.NoAlternative, _service.Reroll(Account, old.InstanceId).Result);
            Assert.AreEqual(old.InstanceId, State("a").InstanceId);
            Assert.AreEqual(1, State("a").Objectives[0].Progress);
            Assert.IsTrue(State("a").CanRefresh);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompletedTaskIsCleanedBeforeDailyFillWhetherRewardWasClaimed(bool claim)
        {
            Start(Master(Def("a", "FormPattern", "[100,\"FullHouse\",1]")), Fixed(), Random(1, 3, "a"));
            _service.Consume(Fact("done"));
            var old = State("a");
            if (claim) Claim(old);
            _service.OnLogin(Account);
            Assert.AreEqual(old.InstanceId, State("a").InstanceId);
            _now = _now.AddDays(1);
            _service.OnLogin(Account);
            var fresh = State("a");
            Assert.AreNotEqual(old.InstanceId, fresh.InstanceId);
            Assert.AreEqual(old.SlotId, fresh.SlotId);
            Assert.IsTrue(fresh.CanRefresh);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
            Assert.AreEqual(DailyTaskResult.AlreadyClaimed, Claim(old).Result);
            _service.OnLogin(Account);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void BlockedCleanupKeepsOutDatedRewardWithoutOccupyingSlotAndClaimDeletesIt()
        {
            Start(Master(Def("a", "FormPattern", "[100,\"FullHouse\",1]")), Fixed(), Random(1, 3, "a"));
            _service.Consume(Fact("done"));
            var old = State("a");
            var access = BeginRewardHand();
            _now = _now.AddDays(1);
            _service.OnLogin(Account);
            var tasks = _service.GetStatus(Account).Tasks;
            Assert.AreEqual(2, tasks.Length);
            var retained = tasks.Single(t => t.InstanceId == old.InstanceId);
            Assert.IsTrue(retained.OutDated);
            Assert.IsFalse(retained.CanRefresh);
            Assert.IsTrue(tasks.Single(t => !t.OutDated).CanRefresh);
            Assert.AreEqual(900, _repository.FindById(Account).Chips);
            Assert.AreEqual(DailyTaskResult.Success, _service.Claim(Account, old.InstanceId, 1, access).Result);
            Assert.AreEqual(1, _service.GetStatus(Account).Tasks.Length);
            Assert.AreEqual(DailyTaskResult.AlreadyClaimed, _service.Claim(Account, old.InstanceId, 1, access).Result);
        }

        [Test]
        public void CleanupAtBalanceLimitRetainsRewardAndContinuesAllocating()
        {
            Start(Master(Def("a", "FormPattern", "[100,\"FullHouse\",1]")), Fixed(), Random(1, 3, "a"));
            _service.Consume(Fact("done"));
            var old = State("a");
            Sql("UPDATE player_accounts SET chips=4294967295 WHERE account_id='TaskTest';");
            _now = _now.AddDays(1);
            _service.OnLogin(Account);
            Assert.AreEqual(2, _service.GetStatus(Account).Tasks.Length);
            Assert.IsTrue(_service.GetStatus(Account).Tasks.Single(t => t.InstanceId == old.InstanceId).OutDated);
            Assert.AreEqual(DailyTaskResult.BalanceLimit, Claim(old).Result);
            Sql("UPDATE player_accounts SET chips=900 WHERE account_id='TaskTest';");
            Assert.AreEqual(DailyTaskResult.Success, Claim(old).Result);
            Assert.AreEqual(1, _service.GetStatus(Account).Tasks.Length);
        }

        [Test]
        public void RefreshPendingRewardIsAtomicAndNewTaskHasNoRemainingRefresh()
        {
            Start(Master(Def("a", "FormPattern", "[100,\"FullHouse\",1]"), Def("b", "FormPattern", "[200,\"FullHouse\",2]")),
                Fixed(), Random(1, 3, "a", "b"));
            _service.Consume(Fact("done"));
            var old = State("a");
            Sql("CREATE TRIGGER refresh_failure BEFORE UPDATE ON task_accounts BEGIN SELECT RAISE(ABORT,'Injected'); END;");
            Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => _service.Reroll(Account, old.InstanceId));
            Assert.AreEqual(900, _repository.FindById(Account).Chips);
            Assert.IsTrue(State("a").CanRefresh);
            Sql("DROP TRIGGER refresh_failure;");
            Assert.AreEqual(DailyTaskResult.Success, _service.Reroll(Account, old.InstanceId).Result);
            Assert.AreEqual(1000, _repository.FindById(Account).Chips);
            Assert.IsFalse(State("b").CanRefresh);
            Assert.AreEqual(0, State("b").ClaimedCount);
        }

        [Test]
        public void TaskProtocolPreservesRefreshOutDatedAndSlot()
        {
            using var writer = NetworkWriterPool.Get();
            writer.Write(new DailyTasksResponse { Snapshot = new DailyTasksSnapshot { Tasks = new[] {
                new DailyTaskState { CanRefresh = true, SlotId = 7, OutDated = true } } } });
            writer.Write(new DailyTaskDeleteRequest { InstanceId = "test", Refresh = true });
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            var state = reader.Read<DailyTasksResponse>().Snapshot.Tasks.Single();
            Assert.IsTrue(state.CanRefresh);
            Assert.IsTrue(state.OutDated);
            Assert.AreEqual(7, state.SlotId);
            Assert.IsTrue(reader.Read<DailyTaskDeleteRequest>().Refresh);
        }
    }
}
