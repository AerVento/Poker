using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using Game.UI;
using Mirror;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private DailyTaskService _dailyTaskService;
        private readonly Dictionary<NetworkConnectionToClient, ulong> _dailyTaskRequestSequences =
            new Dictionary<NetworkConnectionToClient, ulong>();
        private ulong _nextDailyTaskRequest;

        public bool CanOpenDailyTasks => HasAuthenticatedClient && !_roomEntryPending;
        public bool CanClaimDailyTasks => CanOpenDailyTasks &&
            (!_activeClientRoomId.HasValue && !GameController.Instance.State.LastRoomId.HasValue ||
             _activeClientRoomId.HasValue && GamePanel.Instance != null &&
             GamePanel.Instance.Info.RoomInfo?.Id == _activeClientRoomId && GamePanel.Instance.CanClaimDailyTaskRewards);

        private TaskRewardAccess TaskAccess(PlayerAccount account)
            => _roomManager.WhichRoom(account.UserName, out var roomId)
                ? _gameManager.GetTaskRewardAccess(roomId, account.AccountId) : null;

        private DailyTaskResult ValidateTaskRewardPhase(PlayerAccount account)
        {
            if (_roomManager.WhichRoom(account.UserName, out var roomId))
                return _gameManager.GetTaskRewardAccess(roomId, account.AccountId) != null ? DailyTaskResult.Success :
                    _gameManager.GetGameInfo(roomId) != null ? DailyTaskResult.ActiveHand : DailyTaskResult.OutsideRoomRequired;
            return account.LastRoomId.HasValue ? DailyTaskResult.OutsideRoomRequired : DailyTaskResult.Success;
        }

        private void SynchronizeTaskRewardBalance(PlayerAccount account)
        {
            if (!_roomManager.WhichRoom(account.UserName, out var roomId)) return;
            _gameManager.RefreshTaskRewardBalance(roomId, account);
            BroadcastGameInfo(roomId);
        }

        private void RegisterDailyTaskServerHandlers()
        {
            _dailyTaskService = authenticator is AccountAuthenticator accountAuthenticator &&
                accountAuthenticator.AccountRepository is IDailyRewardRepository repository
                ? new DailyTaskService(repository) : null;
            InitializeConfiguredTasks();
            if (_serverStorageFailed) return;
            _dailyTaskRequestSequences.Clear();
            RegisterServerHandler<DailyTaskDeleteRequest>((conn, request) =>
            {
                var response = ProcessDailyTaskDelete(conn, request, out var account);
                if (account != null && (response.Result == DailyTaskResult.Success || response.Result == DailyTaskResult.AlreadyDeleted))
                {
                    if (_playerManager.TryGetSession(conn, out var session)) session.Account = account.Copy();
                    SynchronizeTaskRewardBalance(account);
                    conn.Send(new AccountBalanceSync { Chips = account.Chips });
                }
                conn.Send(response);
            }, requireAuthentication: true);
            RegisterServerHandler<DailyTasksRequest>((conn, request) =>
                conn.Send(ProcessDailyTasksRequest(conn, request)), requireAuthentication: true);
            RegisterServerHandler<DailyTaskClaimRequest>((conn, request) =>
            {
                var response = ProcessDailyTaskClaim(conn, request, out var account);
                if (account != null && (response.Result == DailyTaskResult.Success ||
                    response.Result == DailyTaskResult.AlreadyClaimed))
                {
                    if (_playerManager.TryGetSession(conn, out var session))
                        session.Account = account.Copy();
                    SynchronizeTaskRewardBalance(account);
                    conn.Send(new AccountBalanceSync { Chips = account.Chips });
                }
                conn.Send(response);
            }, requireAuthentication: true);
        }

        private DailyTaskResult ValidateDailyTaskRequest(NetworkConnectionToClient conn, ulong requestId,
            out PlayerAccount account)
        {
            account = null;
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated || !_playerManager.TryGetAccount(conn, out account))
                return DailyTaskResult.Unavailable;
            if (requestId == 0 || (_dailyTaskRequestSequences.TryGetValue(conn, out var last) && requestId <= last))
                return DailyTaskResult.InvalidRequest;
            _dailyTaskRequestSequences[conn] = requestId;
            return _configuredTasks == null && _dailyTaskService == null ? DailyTaskResult.Unavailable : DailyTaskResult.Success;
        }

        internal DailyTasksResponse ProcessDailyTasksRequest(NetworkConnectionToClient conn, DailyTasksRequest request)
        {
            var result = ValidateDailyTaskRequest(conn, request.RequestId, out var account);
            if (request.Offset < 0 || request.Offset > 1000000 || request.Revision < 0)
                result = DailyTaskResult.InvalidRequest;
            return new DailyTasksResponse
            {
                RequestId = request.RequestId,
                AccountId = account?.AccountId,
                Result = result,
                Snapshot = result == DailyTaskResult.Success ? (_configuredTasks != null ? ReadConfiguredTasks(account.AccountId, request, TaskAccess(account)) :
                    _dailyTaskService.GetStatus(account.AccountId)) : default,
            };
        }

        internal DailyTaskClaimResponse ProcessDailyTaskClaim(NetworkConnectionToClient conn,
            DailyTaskClaimRequest request, out PlayerAccount updatedAccount)
        {
            updatedAccount = null;
            var result = ValidateDailyTaskRequest(conn, request.RequestId, out var account);
            if (result == DailyTaskResult.Success) result = ValidateTaskRewardPhase(account);
            TaskClaimReceipt receipt = null;
            if (result == DailyTaskResult.Success && _configuredTasks != null)
            {
                var outcome = ClaimConfiguredTask(account.AccountId, request, TaskAccess(account));
                result = outcome.Result;
                receipt = outcome.Receipt;
                updatedAccount = outcome.Account;
            }
            else if (result == DailyTaskResult.Success)
                result = _dailyTaskService.Claim(account.AccountId, request.TaskId, request.Period, out updatedAccount);
            return new DailyTaskClaimResponse
            {
                RequestId = request.RequestId,
                AccountId = account?.AccountId,
                TaskId = request.TaskId,
                Result = result,
                InstanceId = receipt?.InstanceId ?? request.InstanceId,
                CompletionOrdinal = receipt?.Ordinal ?? request.CompletionOrdinal,
                GrantedItems = receipt?.Items?.Select(i => new TaskRewardItemState { ItemId = i.ItemId, Count = i.Count }).ToArray(),
                GrantedAmount = result == DailyTaskResult.Success
                    ? receipt?.Chip ?? (request.TaskId == DailyTaskId.DailySupply ? DailyTaskService.SupplyReward : DailyTaskService.ComebackReward)
                    : 0,
            };
        }

        public ulong RequestDailyTasks()
        {
            if (!CanOpenDailyTasks)
                return 0;
            var requestId = ++_nextDailyTaskRequest;
            NetworkClient.Send(new DailyTasksRequest { RequestId = requestId });
            return requestId;
        }

        public ulong RequestDailyTaskClaim(DailyTaskId taskId, int period)
        {
            if (!CanClaimDailyTasks)
                return 0;
            var requestId = ++_nextDailyTaskRequest;
            NetworkClient.Send(new DailyTaskClaimRequest { RequestId = requestId, TaskId = taskId, Period = period });
            return requestId;
        }

        private void RegisterDailyTaskClientHandlers()
        {
            NetworkClient.RegisterHandler<DailyTaskDeleteResponse>(DailyTaskDeleteResponseHandler);
            NetworkClient.RegisterHandler<DailyTasksResponse>(DailyTasksResponseHandler);
            NetworkClient.RegisterHandler<DailyTaskClaimResponse>(DailyTaskClaimResponseHandler);
        }

        private void DailyTasksResponseHandler(DailyTasksResponse response)
        {
            if (CanOpenDailyTasks && response.AccountId == GameController.Instance.State.AccountId)
                DailyTaskPanel.Instance?.ReceiveStatus(response);
        }

        private void DailyTaskClaimResponseHandler(DailyTaskClaimResponse response)
        {
            if (CanOpenDailyTasks && response.AccountId == GameController.Instance.State.AccountId)
                DailyTaskPanel.Instance?.ReceiveClaim(response);
        }

        internal DailyTaskDeleteResponse ProcessDailyTaskDelete(NetworkConnectionToClient conn, DailyTaskDeleteRequest request,
            out PlayerAccount updatedAccount)
        {
            updatedAccount = null;
            var result = ValidateDailyTaskRequest(conn, request.RequestId, out var account);
            if (result == DailyTaskResult.Success) result = ValidateTaskRewardPhase(account);
            Game.Network.Tasks.TaskDeleteOutcome outcome = null;
            if (result == DailyTaskResult.Success)
            {
                if (_configuredTasks == null) result = DailyTaskResult.Unavailable;
                else
                {
                    RefreshTaskDay();
                    _configuredTasks.DrainFacts();
                    outcome = request.Refresh ? _configuredTasks.Reroll(account.AccountId, request.InstanceId, TaskAccess(account)) :
                        _configuredTasks.Delete(account.AccountId, request.InstanceId, TaskAccess(account));
                    result = outcome.Result;
                    updatedAccount = outcome.Account;
                }
            }
            return new DailyTaskDeleteResponse { Refreshed = request.Refresh, RequestId = request.RequestId, AccountId = account?.AccountId,
                InstanceId = request.InstanceId, Result = result, GrantedAmount = outcome?.AutoClaim?.Chip ?? 0,
                GrantedItems = outcome?.AutoClaim?.Items?.Select(i => new TaskRewardItemState { ItemId = i.ItemId, Count = i.Count }).ToArray() };
        }

        public ulong RequestDailyTaskDelete(string instanceId, bool refresh = false)
        {
            if (!CanClaimDailyTasks) return 0;
            var requestId = ++_nextDailyTaskRequest;
            NetworkClient.Send(new DailyTaskDeleteRequest { RequestId = requestId, InstanceId = instanceId, Refresh = refresh });
            return requestId;
        }

        private void DailyTaskDeleteResponseHandler(DailyTaskDeleteResponse response)
        {
            if (CanOpenDailyTasks && response.AccountId == GameController.Instance.State.AccountId)
                DailyTaskPanel.Instance?.ReceiveDelete(response);
        }
    }
}
