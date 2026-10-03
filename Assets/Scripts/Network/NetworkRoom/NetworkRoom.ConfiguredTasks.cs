using System;
using System.Linq;
using Game.Network.Accounts;
using Game.Network.Tasks;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private ConfiguredTaskService _configuredTasks;
        private int _taskBusinessPeriod;
        private double _nextTaskTick;

        private void InitializeConfiguredTasks()
        {
            try
            {
                var repository = (authenticator as AccountAuthenticator)?.AccountRepository as ITaskRepository;
                if (repository == null) throw new InvalidOperationException("Configured tasks require task storage.");
                _configuredTasks = new ConfiguredTaskService(repository, ServerConfiguration.Current.Tasks);
                _configuredTasks.Initialize();
                _taskBusinessPeriod = TaskPeriod.GetPeriod(DateTimeOffset.UtcNow);
            }
            catch (Exception exception) { HandleFatalStorageError(exception); }
        }

        public override void Update()
        {
            base.Update();
            TickAchievements();
            if (!NetworkServer.active || _configuredTasks == null || _serverStorageFailed || Time.realtimeSinceStartupAsDouble < _nextTaskTick) return;
            _nextTaskTick = Time.realtimeSinceStartupAsDouble + 0.1;
            try { RefreshTaskDay(); _configuredTasks.DrainFacts(); }
            catch (Exception exception) { HandleFatalStorageError(exception); }
        }

        private void RefreshTaskDay()
        {
            var period = TaskPeriod.GetPeriod(DateTimeOffset.UtcNow);
            if (_configuredTasks == null || _taskBusinessPeriod == period) return;
            _configuredTasks.RefreshAllFixed();
            foreach (var connection in NetworkServer.connections.Values)
                if (connection != null && connection.isAuthenticated && _playerManager.TryGetAccount(connection, out var account))
                {
                    _configuredTasks.OnLogin(account.AccountId);
                    SendAccountBalance(connection, account.UserName);
                }
            _taskBusinessPeriod = period;
        }

        private DailyTasksSnapshot ReadConfiguredTasks(string accountId, DailyTasksRequest request)
        {
            RefreshTaskDay();
            _configuredTasks.DrainFacts();
            return _configuredTasks.GetStatus(accountId, request.Offset, request.Revision);
        }

        private TaskClaimOutcome ClaimConfiguredTask(string accountId, DailyTaskClaimRequest request)
        {
            RefreshTaskDay();
            _configuredTasks.DrainFacts();
            if (string.IsNullOrEmpty(request.InstanceId))
                return new TaskClaimOutcome { Result = DailyTaskResult.InvalidRequest };
            return _configuredTasks.Claim(accountId, request.InstanceId, request.CompletionOrdinal);
        }

        public ulong RequestDailyTasksPage(int offset, long revision)
        {
            if (!CanOpenDailyTasks) return 0;
            var requestId = ++_nextDailyTaskRequest;
            NetworkClient.Send(new DailyTasksRequest { RequestId = requestId, Offset = offset, Revision = revision });
            return requestId;
        }

        public ulong RequestDailyTaskClaim(string instanceId, int ordinal)
        {
            if (!CanClaimDailyTasks) return 0;
            var requestId = ++_nextDailyTaskRequest;
            NetworkClient.Send(new DailyTaskClaimRequest { RequestId = requestId, InstanceId = instanceId, CompletionOrdinal = ordinal });
            return requestId;
        }
    }
}
