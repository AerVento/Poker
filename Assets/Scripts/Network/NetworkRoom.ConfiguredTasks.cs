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
                _configuredTasks = new ConfiguredTaskService(repository, ServerTaskConfigurationLoader.Load(_itemRuntime));
                _configuredTasks.Initialize();
                _taskBusinessPeriod = DailyTaskService.GetPeriod(DateTimeOffset.UtcNow);
            }
            catch (Exception exception) { HandleFatalStorageError(exception); }
        }

        public override void Update()
        {
            base.Update();
            if (!NetworkServer.active || _configuredTasks == null || _serverStorageFailed || Time.realtimeSinceStartupAsDouble < _nextTaskTick) return;
            _nextTaskTick = Time.realtimeSinceStartupAsDouble + 0.1;
            try { RefreshTaskDay(); _configuredTasks.DrainFacts(); }
            catch (Exception exception) { HandleFatalStorageError(exception); }
        }

        private void RefreshTaskDay()
        {
            var period = DailyTaskService.GetPeriod(DateTimeOffset.UtcNow);
            if (_configuredTasks == null || _taskBusinessPeriod == period) return;
            _configuredTasks.RefreshAllFixed();
            foreach (var connection in NetworkServer.connections.Values)
                if (connection != null && connection.isAuthenticated && _playerManager.TryGetAccount(connection, out var account))
                    _configuredTasks.OnLogin(account.AccountId);
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
            var instanceId = request.InstanceId;
            var ordinal = request.CompletionOrdinal;
            if (string.IsNullOrEmpty(instanceId))
            {
                // Legacy packets may only claim the first round of today's fixed welfare.
                if (request.Period != DailyTaskService.GetPeriod(DateTimeOffset.UtcNow))
                    return new TaskClaimOutcome { Result = DailyTaskResult.PeriodChanged };
                if (request.TaskId != DailyTaskId.DailySupply && request.TaskId != DailyTaskId.Comeback)
                    return new TaskClaimOutcome { Result = DailyTaskResult.InvalidRequest };
                var state = _configuredTasks.GetStatus(accountId).Tasks.FirstOrDefault(t => t.Id == request.TaskId && !t.IsRandom &&
                    t.ExpiresAtUnixSeconds > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                instanceId = state.InstanceId;
                ordinal = 1;
            }
            return _configuredTasks.Claim(accountId, instanceId, ordinal);
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
            if (!CanOpenDailyTasks) return 0;
            var requestId = ++_nextDailyTaskRequest;
            NetworkClient.Send(new DailyTaskClaimRequest { RequestId = requestId, InstanceId = instanceId, CompletionOrdinal = ordinal });
            return requestId;
        }
    }
}
