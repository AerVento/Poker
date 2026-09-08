using System;
using Game.Facts;

namespace Game.Network.Accounts
{
    public sealed partial class GuardedAccountRepository : ITaskRepository
    {
        private ITaskRepository Tasks => _inner as ITaskRepository
            ?? throw new InvalidOperationException("The account repository does not support configured tasks.");
        public void InitializeTasks() => Execute(() => { Tasks.InitializeTasks(); return true; });
        public string[] GetTaskAccountIds() => Execute(() => Tasks.GetTaskAccountIds());
        public T TransactTasks<T>(string accountId, Func<TaskAccountTransaction, T> operation, string eventId = null)
            => Execute(() => Tasks.TransactTasks(accountId, operation, eventId));
        public void PublishHandFact(HandFact fact)
        {
            if (_inner is IHandFactPublisher publisher)
                Execute(() => { publisher.PublishHandFact(fact); return true; });
        }
        public HandFact[] ReadPendingHandFacts(int limit) => Execute(() => Tasks.ReadPendingHandFacts(limit));
    }
}
