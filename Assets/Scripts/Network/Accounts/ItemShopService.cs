using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Game.Network.Accounts
{
    public sealed class ItemEffectOutcome
    {
        public int EffectIndex { get; }
        public int CommandIndex { get; }
        public PurchaseEffectKind Kind { get; }
        public int Version { get; }
        public uint Amount { get; }
        public uint BalanceBefore { get; }
        public uint BalanceAfter { get; }

        [JsonConstructor]
        public ItemEffectOutcome(int effectIndex, int commandIndex, PurchaseEffectKind kind, int version,
            uint amount, uint balanceBefore, uint balanceAfter)
        {
            EffectIndex = effectIndex;
            CommandIndex = commandIndex;
            Kind = kind;
            Version = version;
            Amount = amount;
            BalanceBefore = balanceBefore;
            BalanceAfter = balanceAfter;
        }
    }

    public sealed class ItemPurchaseOutcome
    {
        public int FormatVersion { get; }
        public string ItemId { get; }
        public uint ChargedPrice { get; }
        public uint BalanceBefore { get; }
        public uint BalanceAfter { get; }
        public string RuleRevision { get; }
        public PurchaseConditionDescriptor FailedCondition { get; }
        public IReadOnlyList<ItemEffectOutcome> Effects { get; }
        [JsonIgnore] public uint RewardChips => checked((uint)Effects.Sum(e => (long)e.Amount));

        [JsonConstructor]
        public ItemPurchaseOutcome(int formatVersion, string itemId, uint chargedPrice, uint balanceBefore,
            uint balanceAfter, string ruleRevision, PurchaseConditionDescriptor failedCondition, IReadOnlyList<ItemEffectOutcome> effects)
        {
            if (formatVersion != 1) throw new InvalidOperationException("Unsupported item receipt version.");
            FormatVersion = formatVersion;
            ItemId = itemId;
            ChargedPrice = chargedPrice;
            BalanceBefore = balanceBefore;
            BalanceAfter = balanceAfter;
            RuleRevision = ruleRevision;
            FailedCondition = failedCondition;
            Effects = Array.AsReadOnly((effects ?? Array.Empty<ItemEffectOutcome>()).ToArray());
        }
    }

    public sealed class ItemChangeExecution
    {
        public ItemShopResult Result { get; }
        public ItemPurchaseOutcome Outcome { get; }
        public bool IsNewlyApplied { get; }

        public ItemChangeExecution(ItemShopResult result, ItemPurchaseOutcome outcome = null, bool isNewlyApplied = false)
        {
            Result = result;
            Outcome = outcome;
            IsNewlyApplied = isNewlyApplied;
        }
    }

    public sealed class ItemPurchased
    {
        public string AccountId { get; }
        public string OperationId { get; }
        public ItemPurchaseOutcome Outcome { get; }

        internal ItemPurchased(string accountId, string operationId, ItemPurchaseOutcome outcome)
        {
            AccountId = accountId;
            OperationId = operationId;
            Outcome = outcome;
        }
    }

    // Best-effort, server-local notifications only. Authority-changing effects belong to the transaction.
    public sealed class ItemPurchaseEvents
    {
        private readonly Dictionary<string, List<Action<ItemPurchased>>> _handlers = new Dictionary<string, List<Action<ItemPurchased>>>(StringComparer.Ordinal);
        private readonly Action<Exception> _onError;

        public ItemPurchaseEvents(Action<Exception> onError = null) => _onError = onError;

        public IDisposable Subscribe(string itemId, Action<ItemPurchased> handler)
        {
            if (!NamecardServerCatalog.IsValidId(itemId) || handler == null) throw new ArgumentException();
            if (!_handlers.TryGetValue(itemId, out var list)) _handlers[itemId] = list = new List<Action<ItemPurchased>>();
            list.Add(handler);
            return new Subscription(() => list.Remove(handler));
        }

        internal void Publish(ItemPurchased notification)
        {
            if (!_handlers.TryGetValue(notification.Outcome.ItemId, out var list)) return;
            foreach (var handler in list.ToArray())
            {
                try { handler(notification); }
                catch (Exception exception)
                {
                    // Error reporting must not turn a committed purchase into a failure either.
                    try { _onError?.Invoke(exception); }
                    catch { }
                }
            }
        }

        public void Clear() => _handlers.Clear();

        private sealed class Subscription : IDisposable
        {
            private Action _remove;
            public Subscription(Action remove) => _remove = remove;
            public void Dispose()
            {
                _remove?.Invoke();
                _remove = null;
            }
        }
    }

    public sealed class ItemShopService
    {
        private readonly IItemRepository _repository;
        private readonly ItemPurchaseRuntime _runtime;
        private readonly ItemPurchaseEvents _events;

        public ItemShopService(IItemRepository repository, ItemPurchaseRuntime runtime, ItemPurchaseEvents events)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public ItemChangeExecution ChangeItem(string accountId, string operationId, ItemShopAction action, string itemId,
            uint quantity, uint quotedPrice, string revision, bool inRoom = false)
        {
            var result = _repository.ChangeItem(accountId, operationId, action, itemId, quantity, quotedPrice, revision, _runtime, inRoom);
            if (result.IsNewlyApplied && action == ItemShopAction.Purchase)
                _events.Publish(new ItemPurchased(accountId, operationId, result.Outcome));
            return result;
        }
    }
}
