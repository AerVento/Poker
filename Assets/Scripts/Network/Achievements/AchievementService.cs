using System;
using System.Linq;
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
        public AchievementCatalog Catalog { get; }

        public AchievementService(IAchievementRepository repository, AchievementCatalog catalog,
            Func<string, uint?> itemLimit, Func<long> now = null)
        {
            _repository = repository; Catalog = catalog; _itemLimit = itemLimit;
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
            var context = entry.Context;
            if (!context.Players.Any(p => p.AccountId == entry.AccountId))
                throw new InvalidOperationException("Achievement account is not a hand participant.");
            _repository.TransactAchievements(entry.AccountId, tx =>
            {
                if (tx.AlreadyConsumed) return false;
                foreach (var definition in Catalog.All)
                {
                    var state = tx.Document.Achievements.Find(a => a.Id == definition.Id);
                    if (state?.CompletedAt > 0) continue;
                    var isNew = state == null;
                    state ??= new PlayerAchievement { Id = definition.Id };
                    var before = JsonSerializer.Serialize(state.Progress);
                    definition.Evaluate(context, entry.AccountId, state.Progress);
                    state.Progress.Count = Math.Max(0, Math.Min(definition.Target, state.Progress.Count));
                    if (before == JsonSerializer.Serialize(state.Progress)) continue;
                    if (isNew) tx.Document.Achievements.Add(state);
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
                return tx.Changed;
            }, context.EventId);
        }

        public AchievementsResponse GetStatus(string accountId, int offset = 0, long revision = 0)
        {
            if (offset < 0 || offset > Catalog.All.Count) return new AchievementsResponse { Result = AchievementResult.InvalidRequest };
            return _repository.TransactAchievements(accountId, tx =>
            {
                var response = new AchievementsResponse { AccountId = accountId, Result = AchievementResult.Success,
                    Balance = tx.Account.Chips, CanClaim = tx.CanClaim, Revision = tx.Document.Revision,
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

        public AchievementClaimResponse Claim(string accountId, string id, out PlayerAccount account)
        {
            account = null;
            if (!AchievementCatalog.IsValidId(id)) return new AchievementClaimResponse { Result = AchievementResult.InvalidRequest };
            PlayerAccount updated = null;
            var result = _repository.TransactAchievements(accountId, tx =>
            {
                var response = new AchievementClaimResponse { AccountId = accountId, AchievementId = id, Result = AchievementResult.NotComplete };
                var state = tx.Document.Achievements.Find(a => a.Id == id);
                if (state == null && Catalog.Find(id) == null) response.Result = AchievementResult.UnknownAchievement;
                if (state == null || state.CompletedAt == 0) return response;
                if (state.ClaimedAt > 0)
                {
                    response.Result = AchievementResult.AlreadyClaimed;
                    updated = tx.Account;
                    return response;
                }
                response.Result = tx.Grant(new Reward(state.RewardChips, state.RewardItems), state.ItemLimits,
                    "AchievementReward:" + accountId + ":" + id);
                if (response.Result != AchievementResult.Success) return response;
                state.ClaimedAt = Math.Max(1, _now());
                tx.Changed = true;
                response.GrantedChips = state.RewardChips;
                response.GrantedItems = state.RewardItems.Select(i => new TaskRewardItemState { ItemId = i.ItemId, Count = i.Quantity }).ToArray();
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

        private static AchievementView Project(Achievement definition, PlayerAchievement state)
        {
            var complete = state?.CompletedAt > 0;
            var items = complete ? state.RewardItems : definition.Reward.Items ?? Array.Empty<ItemStack>();
            return new AchievementView
            {
                Id = definition.Id, Name = definition.Name, Description = definition.Description, Category = definition.Category,
                IconPath = definition.IconPath, Progress = complete ? definition.Target : state?.Progress.Count ?? 0,
                Target = definition.Target, CompletedAt = state?.CompletedAt ?? 0, ClaimedAt = state?.ClaimedAt ?? 0,
                RewardChips = complete ? state.RewardChips : definition.Reward.Chip,
                RewardItems = items.Select(i => new TaskRewardItemState { ItemId = i.ItemId, Count = i.Quantity }).ToArray()
            };
        }
    }
}
