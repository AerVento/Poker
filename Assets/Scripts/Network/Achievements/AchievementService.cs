using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Achievements;
using Game.Network.Accounts;
using Game.Tasks;

namespace Game.Network.Achievements
{
    public sealed class AchievementService
    {
        public const int PageSize = 32;
        private readonly IAchievementRepository _repository;
        private readonly Func<string, uint?> _itemLimit;
        private readonly Func<long> _now;
        private readonly AchievementEventRouter _router;
        public AchievementCatalog Catalog { get; }
        public string CatalogRevision { get; }
        private readonly AchievementDefinitionView[] _definitions;
        public event Action<string> ProgressChanged;

        public AchievementService(IAchievementRepository repository, AchievementCatalog catalog,
            Func<string, uint?> itemLimit, Func<long> now = null)
        {
            _repository = repository; Catalog = catalog; _itemLimit = itemLimit;
            _router = new AchievementEventRouter(catalog);
            var fullDefinitions = catalog.All.Select(d => ProjectDefinition(d, false)).ToArray();
            _definitions = catalog.All.Select(d => ProjectDefinition(d, true)).ToArray();
            using (var hash = SHA256.Create())
                CatalogRevision = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(fullDefinitions, new JsonSerializerOptions { IncludeFields = true })))).Replace("-", "");
            _now = now ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            foreach (var achievement in catalog.All)
                foreach (var item in achievement.Reward.Items ?? Array.Empty<ItemStack>())
                    if (!_itemLimit(item.ItemId).HasValue || item.Quantity > _itemLimit(item.ItemId).Value)
                        throw new InvalidOperationException("Invalid achievement reward item: " + item.ItemId);
        }

        public void Initialize() => _repository.InitializeAchievements();

        public int DrainEvents(int limit = 100)
        {
            var pending = _repository.ReadPendingAchievementEvents(limit);
            foreach (var entry in pending) Consume(entry);
            return pending.Length;
        }

        public void Consume(PendingAchievementEvent entry)
        {
            var context = entry.Event ?? throw new InvalidOperationException("Missing player event.");
            if (!context.AccountIds.Contains(entry.AccountId))
                throw new InvalidOperationException("Achievement account is not an event recipient.");
            var visibleChanged = _repository.TransactAchievements(entry.AccountId, tx =>
            {
                if (tx.AlreadyConsumed) return false;
                var changed = false;
                foreach (var binding in _router.For(context))
                {
                    var definition = binding.Definition;
                    var state = tx.Document.Achievements.Find(a => a.Id == definition.Id);
                    if (state?.CompletedAt > 0) continue;
                    var isNew = state == null;
                    state ??= new PlayerAchievement { Id = definition.Id };
                    var beforeCount = state.Progress.Count;
                    var before = JsonSerializer.Serialize(state.Progress);
                    binding.Evaluate(context, entry.AccountId, state.Progress);
                    state.Progress.Count = Math.Max(0, Math.Min(definition.Target, state.Progress.Count));
                    if (before == JsonSerializer.Serialize(state.Progress)) continue;
                    if (isNew) tx.Document.Achievements.Add(state);
                    changed |= beforeCount != state.Progress.Count;
                    if (state.Progress.Count == definition.Target)
                    {
                        state.CompletedAt = Math.Max(1, context.OccurredAt);
                        state.RewardChips = definition.Reward.Chip;
                        state.RewardItems = (definition.Reward.Items ?? Array.Empty<ItemStack>()).ToArray();
                        state.ItemLimits = state.RewardItems.ToDictionary(i => i.ItemId, i => _itemLimit(i.ItemId)
                            ?? throw new InvalidOperationException("Achievement item was removed: " + i.ItemId));
                        state.Progress.Data.Clear();
                    }
                    tx.Changed = true;
                }
                return changed;
            }, context.EventId);
            // Notify only after the durable transaction commits; scratch-state changes need no UI refresh.
            if (visibleChanged) ProgressChanged?.Invoke(entry.AccountId);
        }

        public AchievementsResponse GetStatus(string accountId, int offset = 0, long revision = 0)
        {
            if (offset < 0 || offset > Catalog.All.Count) return new AchievementsResponse { Result = AchievementResult.InvalidRequest };
            return _repository.TransactAchievements(accountId, tx =>
            {
                var response = new AchievementsResponse { AccountId = accountId, Result = AchievementResult.Success,
                    Dollars = tx.Account.Dollars, CanClaim = tx.CanClaim, Revision = tx.Document.Revision,
                    Offset = offset, Total = Catalog.All.Count, NextOffset = -1 };
                if (offset > 0 && revision != tx.Document.Revision)
                {
                    response.Result = AchievementResult.RevisionChanged;
                    return response;
                }
                response.Achievements = Catalog.All.Skip(offset).Take(PageSize)
                    .Select(d => Project(d, tx.Document.Achievements.Find(a => a.Id == d.Id))).ToArray();
                if (offset + response.Achievements.Length < Catalog.All.Count) response.NextOffset = offset + response.Achievements.Length;
                return response;
            });
        }

        public AchievementCatalogResponse GetDefinitions(string accountId, int offset = 0, string revision = null)
        {
            var response = new AchievementCatalogResponse { AccountId = accountId, Revision = CatalogRevision,
                Offset = offset, NextOffset = -1, Total = _definitions.Length };
            if (offset < 0 || offset > _definitions.Length) response.Result = AchievementResult.InvalidRequest;
            else if (offset > 0 && revision != CatalogRevision) response.Result = AchievementResult.RevisionChanged;
            else
            {
                response.Result = AchievementResult.Success;
                response.Definitions = _definitions.Skip(offset).Take(PageSize).ToArray();
                if (offset + response.Definitions.Length < response.Total) response.NextOffset = offset + response.Definitions.Length;
            }
            return response;
        }

        public AchievementStatesResponse GetStates(string accountId, int offset = 0, long revision = 0, string catalogRevision = null)
        {
            if (offset < 0 || offset > Catalog.All.Count)
                return new AchievementStatesResponse { AccountId = accountId, Result = AchievementResult.InvalidRequest };
            return _repository.TransactAchievements(accountId, tx =>
            {
                var response = new AchievementStatesResponse { AccountId = accountId, Result = AchievementResult.Success,
                    Revision = tx.Document.Revision, CatalogRevision = CatalogRevision,
                    Offset = offset, Total = Catalog.All.Count, NextOffset = -1 };
                if (offset > 0 && (revision != response.Revision || catalogRevision != CatalogRevision))
                {
                    response.Result = AchievementResult.RevisionChanged;
                    return response;
                }
                response.States = Catalog.All.Skip(offset).Take(PageSize)
                    .Select(d => ProjectState(d, tx.Document.Achievements.Find(a => a.Id == d.Id))).ToArray();
                if (offset + response.States.Length < response.Total) response.NextOffset = offset + response.States.Length;
                return response;
            });
        }

        public AchievementClaimResponse Claim(string accountId, string id, out PlayerAccount account)
        {
            account = null;
            if (!AchievementCatalog.IsValidId(id)) return new AchievementClaimResponse { Result = AchievementResult.InvalidRequest };
            PlayerAccount updated = null;
            var result = _repository.TransactAchievements(accountId, tx =>
            {
                var response = new AchievementClaimResponse { AccountId = accountId, AchievementId = id, Result = AchievementResult.NotComplete };
                var definition = Catalog.Find(id);
                var state = tx.Document.Achievements.Find(a => a.Id == id);
                if (definition == null)
                {
                    response.Result = AchievementResult.UnknownAchievement;
                    return response;
                }
                if (state == null || state.CompletedAt == 0) return response;
                if (state.ClaimedAt > 0)
                {
                    response.Result = AchievementResult.AlreadyClaimed;
                    response.HasState = true;
                    response.State = ProjectState(definition, state);
                    response.Revision = tx.Document.Revision;
                    updated = tx.Account;
                    return response;
                }
                response.Result = tx.Grant(new Reward(state.RewardChips, state.RewardItems),
                    state.ItemLimits ?? new Dictionary<string, uint>(),
                    "AchievementReward:" + accountId + ":" + id);
                if (response.Result != AchievementResult.Success) return response;
                state.ClaimedAt = Math.Max(1, _now());
                tx.Changed = true;
                response.HasState = true;
                response.State = ProjectState(definition, state);
                response.Revision = checked(tx.Document.Revision + 1);
                response.GrantedDollars = state.RewardChips;
                response.GrantedItems = (state.RewardItems ?? Array.Empty<ItemStack>()).Select(i =>
                    new TaskRewardItemState { ItemId = i.ItemId, Count = i.Quantity }).ToArray();
                updated = tx.Account;
                return response;
            });
            account = updated;
            return result;
        }

        public AchievementBatchClaimResponse ClaimAll(string accountId, string category, out PlayerAccount account)
        {
            account = null;
            category = string.IsNullOrEmpty(category) ? null : category;
            if (category != null && !Catalog.All.Any(definition => definition.Category == category))
                return new AchievementBatchClaimResponse { AccountId = accountId, Category = category,
                    Result = AchievementResult.InvalidRequest, States = Array.Empty<AchievementStateView>(),
                    GrantedItems = Array.Empty<TaskRewardItemState>() };

            PlayerAccount updated = null;
            var result = _repository.TransactAchievements(accountId, tx =>
            {
                var definitions = Catalog.All.Where(definition => category == null || definition.Category == category)
                    .ToDictionary(definition => definition.Id, StringComparer.Ordinal);
                var completed = tx.Document.Achievements.Where(state => state.CompletedAt > 0 && definitions.ContainsKey(state.Id)).ToArray();
                var claimable = completed.Where(state => state.ClaimedAt == 0).ToArray();
                var response = new AchievementBatchClaimResponse
                {
                    AccountId = accountId,
                    Category = category,
                    Result = completed.Length > 0 ? AchievementResult.AlreadyClaimed : AchievementResult.NotComplete,
                    Revision = tx.Document.Revision,
                    States = Array.Empty<AchievementStateView>(),
                    GrantedItems = Array.Empty<TaskRewardItemState>()
                };
                if (claimable.Length == 0) return response;

                ulong dollars = 0;
                var items = new List<ItemStack>();
                var limits = new Dictionary<string, uint>(StringComparer.Ordinal);
                foreach (var state in claimable)
                {
                    dollars += state.RewardChips;
                    if (dollars > uint.MaxValue)
                    {
                        response.Result = AchievementResult.BalanceLimit;
                        return response;
                    }
                    items.AddRange(state.RewardItems ?? Array.Empty<ItemStack>());
                    foreach (var pair in state.ItemLimits ?? new Dictionary<string, uint>())
                    {
                        if (limits.TryGetValue(pair.Key, out var existing)) limits[pair.Key] = Math.Min(existing, pair.Value);
                        else limits.Add(pair.Key, pair.Value);
                    }
                }

                var reward = new Reward((uint)dollars, items);
                response.Result = tx.Grant(reward, limits,
                    "AchievementRewardBatch:" + accountId + ":" + (category ?? "all") + ":" +
                    string.Join(",", claimable.Select(state => state.Id).OrderBy(id => id, StringComparer.Ordinal)));
                if (response.Result != AchievementResult.Success) return response;

                var claimedAt = Math.Max(1, _now());
                foreach (var state in claimable) state.ClaimedAt = claimedAt;
                tx.Changed = true;
                response.ClaimedCount = claimable.Length;
                response.GrantedDollars = (uint)dollars;
                response.GrantedItems = reward.Items.Select(item => new TaskRewardItemState
                    { ItemId = item.ItemId, Count = item.Quantity }).ToArray();
                response.Revision = checked(tx.Document.Revision + 1);
                response.States = claimable.Select(state => ProjectState(definitions[state.Id], state)).ToArray();
                updated = tx.Account;
                return response;
            });
            account = updated;
            return result;
        }

        public AchievementView[] PendingAnnouncements(string accountId) => _repository.TransactAchievements(accountId, tx =>
            tx.Document.Achievements.Where(a => a.CompletedAt > 0 && !a.Announced && Catalog.Find(a.Id) != null)
                .OrderBy(a => a.CompletedAt).ThenBy(a => a.Id, StringComparer.Ordinal).Take(PageSize)
                .Select(a => Project(Catalog.Find(a.Id), a)).ToArray());

        public void Acknowledge(string accountId, string id)
        {
            if (!AchievementCatalog.IsValidId(id)) return;
            _repository.TransactAchievements(accountId, tx =>
            {
                var state = tx.Document.Achievements.Find(a => a.Id == id);
                if (state == null || state.CompletedAt == 0 || state.Announced) return false;
                state.Announced = true;
                tx.Changed = true;
                return true;
            });
        }

        private static AchievementStateView ProjectState(Achievement definition, PlayerAchievement state)
        {
            var complete = state?.CompletedAt > 0;
            var hidden = definition.Hidden && !complete;
            return new AchievementStateView
            {
                Id = state?.Id ?? definition.Id,
                Progress = hidden ? 0 : complete ? definition.Target : state?.Progress.Count ?? 0,
                CompletedAt = state?.CompletedAt ?? 0, ClaimedAt = state?.ClaimedAt ?? 0,
                RewardDollars = complete ? state.RewardChips : 0,
                RewardItems = complete ? (state.RewardItems ?? Array.Empty<ItemStack>()).Select(i => new TaskRewardItemState
                    { ItemId = i.ItemId, Count = i.Quantity }).ToArray() : Array.Empty<TaskRewardItemState>(),
                HasRevealedDefinition = complete && definition.Hidden,
                RevealedDefinition = complete && definition.Hidden ? ProjectDefinition(definition, false) : default
            };
        }

        private static AchievementView Project(Achievement definition, PlayerAchievement state)
        {
            var complete = state?.CompletedAt > 0;
            var hidden = definition.Hidden && !complete;
            var items = complete ? state.RewardItems ?? Array.Empty<ItemStack>() :
                definition.Reward.Items ?? Array.Empty<ItemStack>();
            return new AchievementView
            {
                Id = definition.Id,
                Name = hidden ? HiddenAchievementPresentation.Name : definition.Name,
                Description = hidden ? HiddenAchievementPresentation.Description : definition.Description,
                Category = definition.Category,
                IconPath = hidden ? HiddenAchievementPresentation.IconPath : definition.IconPath,
                Progress = hidden ? 0 : complete ? definition.Target : state?.Progress.Count ?? 0,
                Target = hidden ? 0 : definition.Target,
                CompletedAt = state?.CompletedAt ?? 0,
                ClaimedAt = state?.ClaimedAt ?? 0,
                Hidden = definition.Hidden,
                RewardDollars = hidden ? 0 : complete ? state.RewardChips : definition.Reward.Chip,
                RewardItems = hidden ? Array.Empty<TaskRewardItemState>() : items.Select(i => new TaskRewardItemState
                    { ItemId = i.ItemId, Count = i.Quantity }).ToArray()
            };
        }

        private static AchievementDefinitionView ProjectDefinition(Achievement definition, bool maskHidden)
        {
            var hidden = maskHidden && definition.Hidden;
            return new AchievementDefinitionView
            {
                Id = definition.Id,
                Name = hidden ? HiddenAchievementPresentation.Name : definition.Name,
                Description = hidden ? HiddenAchievementPresentation.Description : definition.Description,
                Category = definition.Category,
                IconPath = hidden ? HiddenAchievementPresentation.IconPath : definition.IconPath,
                Target = hidden ? 0 : definition.Target,
                Hidden = definition.Hidden,
                RewardDollars = hidden ? 0 : definition.Reward.Chip,
                RewardItems = hidden ? Array.Empty<TaskRewardItemState>() :
                    (definition.Reward.Items ?? Array.Empty<ItemStack>()).Select(item => new TaskRewardItemState
                        { ItemId = item.ItemId, Count = item.Quantity }).ToArray()
            };
        }
    }
}
