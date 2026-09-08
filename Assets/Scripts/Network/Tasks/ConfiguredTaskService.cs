using System;
using System.Collections.Generic;
using System.Linq;
using Game.Facts;
using Game.Network.Accounts;
using Game.Tasks;

namespace Game.Network.Tasks
{
    public sealed class TaskClaimOutcome
    {
        public DailyTaskResult Result { get; set; }
        public PlayerAccount Account { get; set; }
        public TaskClaimReceipt Receipt { get; set; }
    }

    public sealed class TaskDeleteOutcome
    {
        public DailyTaskResult Result { get; set; }
        public PlayerAccount Account { get; set; }
        public TaskClaimReceipt AutoClaim { get; set; }
    }

    public sealed class ConfiguredTaskService
    {
        public const int PageSize = 40;
        private readonly ITaskRepository _repository;
        private readonly TaskCatalog _catalog;
        private readonly IWeightedTaskSelector _selector;
        private readonly Func<DateTimeOffset> _now;
        private readonly Dictionary<string, TaskDefinition> _definitions = new Dictionary<string, TaskDefinition>();
        public TaskCatalog Catalog => _catalog;

        public ConfiguredTaskService(ITaskRepository repository, TaskCatalog catalog,
            Func<DateTimeOffset> now = null, IWeightedTaskSelector selector = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _now = now ?? (() => DateTimeOffset.UtcNow);
            _selector = selector ?? new WeightedTaskSelector();
        }

        public void Initialize()
        {
            _repository.InitializeTasks();
            RefreshAllFixed();
        }

        public void RefreshAllFixed()
        {
            var now = _now();
            foreach (var accountId in _repository.GetTaskAccountIds())
                _repository.TransactTasks(accountId, tx => { Refresh(tx, now, false); return true; });
        }

        public void OnLogin(string accountId)
        {
            var now = _now();
            _repository.TransactTasks(accountId, tx => { Refresh(tx, now, true); return true; });
        }

        private void Refresh(TaskAccountTransaction tx, DateTimeOffset now, bool fillRandom)
        {
            var document = tx.Document;
            var period = DailyTaskService.GetPeriod(now);
            var timestamp = now.ToUnixTimeMilliseconds();
            if (document.FixedPeriod != period)
            {
                document.Instances.RemoveAll(i => i.ExpiresAt <= timestamp && !i.State.HasPendingReward);
                foreach (var entry in _catalog.Fixed.Where(e => e.Enabled).OrderBy(e => e.SortOrder).ThenBy(e => e.TaskId))
                {
                    var instance = Create(entry, false, now);
                    var legacyId = LegacyId(instance.DefinitionId);
                    if (legacyId != 0 && tx.HasLegacyClaim(DailyTaskService.ClaimKey(tx.Account.AccountId, legacyId, period)))
                    {
                        instance.State.CompletedCount = 1;
                        instance.State.ClaimedCount = 1;
                        tx.Document.Receipts.Add(new TaskClaimReceipt
                        {
                            InstanceId = instance.InstanceId, Ordinal = 1,
                            Chip = Definition(instance).Rule.GetReward().Chip, Items = Array.Empty<TaskRewardItem>()
                        });
                    }
                    document.Instances.Add(instance);
                }
                document.FixedPeriod = period;
                tx.Changed = true;
            }
            if (!fillRandom || document.RandomPeriod == period) return;
            var occupied = document.Instances.Where(i => i.IsRandom && i.ExpiresAt > timestamp &&
                i.State.CompletedCount < i.State.MaxCompletions).ToArray();
            var ids = new HashSet<string>(occupied.Select(i => i.DefinitionId), StringComparer.Ordinal);
            foreach (var entry in _selector.Select(_catalog.Random.Where(e => !ids.Contains(e.TaskId)),
                Math.Max(0, _catalog.Capacity - occupied.Length)))
                document.Instances.Add(Create(entry, true, now));
            document.RandomPeriod = period;
            tx.Changed = true;
        }

        private PlayerTaskInstance Create(TaskPoolEntry entry, bool random, DateTimeOffset now)
        {
            var definition = _catalog.Definitions[entry.TaskId];
            return new PlayerTaskInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"), DefinitionId = definition.Id, DefinitionJson = definition.RawJson,
                Revision = _catalog.Revision, IsRandom = random, DisplayOrder = entry.SortOrder, Period = DailyTaskService.GetPeriod(now),
                AssignedAt = now.ToUnixTimeMilliseconds(),
                ExpiresAt = new DateTimeOffset(now.ToOffset(DailyTaskService.ResetOffset).Date.AddDays(random ? entry.ValidityDays : 1),
                    DailyTaskService.ResetOffset).ToUnixTimeMilliseconds(),
                State = new TaskState { MaxCompletions = definition.MaxCompletions, Progress = new int[definition.Rule.Objectives.Count] },
                ItemLimits = definition.Rule.GetReward().Items.ToDictionary(i => i.ItemId, i => _catalog.ItemLimits[i.ItemId])
            };
        }

        private TaskDefinition Definition(PlayerTaskInstance instance)
        {
            if (!_definitions.TryGetValue(instance.DefinitionJson, out var definition))
            {
                definition = TaskFactory.Compile(instance.DefinitionJson);
                _definitions.Add(instance.DefinitionJson, definition);
            }
            return definition;
        }

        public int DrainFacts(int limit = 100)
        {
            var facts = _repository.ReadPendingHandFacts(limit);
            foreach (var fact in facts) Consume(fact);
            return facts.Length;
        }

        public void Consume(HandFact fact)
        {
            if (fact == null || string.IsNullOrEmpty(fact.EventId) || string.IsNullOrEmpty(fact.HandId) ||
                string.IsNullOrEmpty(fact.AccountId)) throw new ArgumentException("Fact identity is required.");
            var now = _now().ToUnixTimeMilliseconds();
            _repository.TransactTasks(fact.AccountId, tx =>
            {
                if (tx.AlreadyConsumed) return false;
                foreach (var instance in tx.Document.Instances)
                {
                    if (instance.AssignedAt > now || instance.ExpiresAt <= now || !instance.State.CanAccumulate) continue;
                    if (Definition(instance).Rule.Evaluate(fact, instance.State)) tx.Changed = true;
                }
                return tx.Changed;
            }, fact.EventId);
        }

        public DailyTasksSnapshot GetStatus(string accountId, int offset = 0, long revision = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            var now = _now();
            return _repository.TransactTasks(accountId, tx =>
            {
                var visible = tx.Document.Instances.Where(i => i.ExpiresAt > now.ToUnixTimeMilliseconds() || i.State.HasPendingReward)
                    .OrderBy(i => i.IsRandom).ThenBy(i => i.DisplayOrder).ThenBy(i => i.AssignedAt).ThenBy(i => i.InstanceId, StringComparer.Ordinal).ToArray();
                if (offset > 0 && revision != tx.Document.Revision)
                    return new DailyTasksSnapshot { Revision = tx.Document.Revision, Tasks = null };
                var page = visible.Skip(offset).Take(PageSize).Select(i => Project(i, tx, now)).ToArray();
                return new DailyTasksSnapshot
                {
                    Period = DailyTaskService.GetPeriod(now), Revision = tx.Document.Revision, Balance = tx.Account.Chips,
                    ServerUnixSeconds = now.ToUnixTimeSeconds(),
                    NextResetUnixSeconds = new DateTimeOffset(now.ToOffset(DailyTaskService.ResetOffset).Date.AddDays(1),
                        DailyTaskService.ResetOffset).ToUnixTimeSeconds(),
                    Tasks = page, Offset = offset, TotalCount = visible.Length,
                    NextOffset = offset + page.Length < visible.Length ? offset + page.Length : -1
                };
            });
        }

        private DailyTaskState Project(PlayerTaskInstance instance, TaskAccountTransaction tx, DateTimeOffset now)
        {
            var definition = Definition(instance);
            var rule = definition.Rule;
            var state = instance.State;
            var reward = rule.GetReward();
            var valid = instance.ExpiresAt > now.ToUnixTimeMilliseconds();
            var available = rule.Available(new TaskContext { IsWithinValidity = valid, PlayerChip = tx.Account.Chips }, state);
            var availability = available ? DailyTaskAvailability.Claimable :
                state.ClaimedCount >= state.MaxCompletions ? DailyTaskAvailability.Claimed :
                !valid ? DailyTaskAvailability.Expired : rule.IsDirectClaim ? DailyTaskAvailability.BalanceTooHigh : DailyTaskAvailability.InProgress;
            if (available)
            {
                if (tx.HasActiveHand) availability = DailyTaskAvailability.ActiveHand;
                else if (tx.Account.LastRoomId.HasValue) availability = DailyTaskAvailability.OutsideRoom;
                else if ((ulong)tx.Account.Chips + reward.Chip > uint.MaxValue) availability = DailyTaskAvailability.BalanceLimit;
            }
            return new DailyTaskState
            {
                Id = LegacyId(definition.Id), InstanceId = instance.InstanceId, DefinitionId = definition.Id,
                Name = definition.Name, Description = definition.Description, Reward = reward.Chip,
                RewardItems = reward.Items.Select(i => new TaskRewardItemState { ItemId = i.ItemId, Count = i.Quantity }).ToArray(),
                IsRandom = instance.IsRandom, IsDirectClaim = rule.IsDirectClaim, Availability = availability,
                CanDelete = instance.IsRandom && (valid || state.HasPendingReward),
                CompletedCount = state.CompletedCount, ClaimedCount = state.ClaimedCount, MaxCompletions = state.MaxCompletions,
                ClaimOrdinal = state.ClaimedCount + 1, ExpiresAtUnixSeconds = instance.ExpiresAt / 1000,
                Objectives = rule.Objectives.Select((o, i) => new TaskObjectiveState
                    { Description = o.Description, Progress = state.Progress[i], Target = o.Target }).ToArray()
            };
        }

        public TaskClaimOutcome Claim(string accountId, string instanceId, int ordinal)
        {
            if (!Guid.TryParseExact(instanceId, "N", out _) || ordinal < 1 || ordinal > 1000)
                return new TaskClaimOutcome { Result = DailyTaskResult.InvalidRequest };
            var now = _now().ToUnixTimeMilliseconds();
            return _repository.TransactTasks(accountId, tx =>
            {
                var outcome = new TaskClaimOutcome { Account = tx.Account, Result = DailyTaskResult.InvalidRequest };
                var prior = tx.Document.Receipts.Find(r => r.InstanceId == instanceId && r.Ordinal == ordinal);
                if (prior != null)
                {
                    outcome.Result = DailyTaskResult.AlreadyClaimed;
                    outcome.Receipt = prior;
                    return outcome;
                }
                var instance = tx.Document.Instances.Find(i => i.InstanceId == instanceId);
                if (instance == null) return outcome;
                var state = instance.State;
                if (ordinal != state.ClaimedCount + 1 || ordinal > state.MaxCompletions) return outcome;
                var valid = instance.ExpiresAt > now;
                var rule = Definition(instance).Rule;
                if (!rule.Available(new TaskContext { IsWithinValidity = valid, PlayerChip = tx.Account.Chips }, state))
                {
                    outcome.Result = !valid ? DailyTaskResult.Expired : rule.IsDirectClaim ? DailyTaskResult.BalanceTooHigh : DailyTaskResult.NotComplete;
                    return outcome;
                }
                outcome.Result = GrantRound(tx, instance, rule, valid, out var receipt);
                outcome.Receipt = receipt;
                return outcome;
            });
        }

        internal static DailyTaskId LegacyId(string id) => id == "daily_supply" ? DailyTaskId.DailySupply :
            id == "comeback" ? DailyTaskId.Comeback : 0;

        private static DailyTaskResult GrantRound(TaskAccountTransaction tx, PlayerTaskInstance instance, GameTask rule,
            bool valid, out TaskClaimReceipt receipt)
        {
            receipt = null;
            var state = instance.State;
            var ordinal = state.ClaimedCount + 1;
            var reward = rule.GetReward();
            var result = tx.Grant(reward, instance.ItemLimits, "TaskReward:" + tx.Account.AccountId + ":" + instance.InstanceId + ":" + ordinal);
            if (result != DailyTaskResult.Success) return result;
            state.ClaimedCount++;
            if (rule.IsDirectClaim) state.CompletedCount++;
            if (valid && state.ClaimedCount < state.MaxCompletions) state.Progress = new int[rule.Objectives.Count];
            receipt = new TaskClaimReceipt
            {
                InstanceId = instance.InstanceId, Ordinal = ordinal, Chip = reward.Chip,
                Items = reward.Items.Select(i => new TaskRewardItem { ItemId = i.ItemId, Count = i.Quantity }).ToArray()
            };
            tx.Document.Receipts.Add(receipt);
            tx.Changed = true;
            return result;
        }

        public TaskDeleteOutcome Delete(string accountId, string instanceId)
        {
            if (!Guid.TryParseExact(instanceId, "N", out _)) return new TaskDeleteOutcome { Result = DailyTaskResult.InvalidRequest };
            var now = _now().ToUnixTimeMilliseconds();
            return _repository.TransactTasks(accountId, tx =>
            {
                var outcome = new TaskDeleteOutcome { Account = tx.Account, Result = DailyTaskResult.InvalidRequest };
                if (tx.Document.DeletedInstanceIds.Contains(instanceId)) { outcome.Result = DailyTaskResult.AlreadyDeleted; return outcome; }
                var instance = tx.Document.Instances.Find(i => i.InstanceId == instanceId);
                if (instance == null) return outcome;
                if (!instance.IsRandom) { outcome.Result = DailyTaskResult.FixedTask; return outcome; }
                if (tx.Account.LastRoomId.HasValue) { outcome.Result = DailyTaskResult.OutsideRoomRequired; return outcome; }
                if (tx.HasActiveHand) { outcome.Result = DailyTaskResult.ActiveHand; return outcome; }
                var valid = instance.ExpiresAt > now;
                if (!valid && !instance.State.HasPendingReward) { outcome.Result = DailyTaskResult.Expired; return outcome; }
                var rule = Definition(instance).Rule;
                if (rule.Available(new TaskContext { IsWithinValidity = valid, PlayerChip = tx.Account.Chips }, instance.State))
                {
                    outcome.Result = GrantRound(tx, instance, rule, valid, out var receipt);
                    if (outcome.Result != DailyTaskResult.Success) return outcome;
                    outcome.AutoClaim = receipt;
                }
                tx.Document.DeletedInstanceIds.Add(instanceId);
                tx.Document.Instances.Remove(instance);
                // Leave RandomPeriod unchanged: deleting never triggers a same-day refill.
                tx.Changed = true;
                outcome.Result = DailyTaskResult.Success;
                return outcome;
            });
        }
    }
}
