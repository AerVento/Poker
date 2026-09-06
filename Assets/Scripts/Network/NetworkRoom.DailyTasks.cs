using System.Collections.Generic;
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

        public bool CanOpenDailyTasks => HasAuthenticatedClient && !_roomEntryPending &&
            !_activeClientRoomId.HasValue && !GameController.Instance.State.LastRoomId.HasValue;

        private void RegisterDailyTaskServerHandlers()
        {
            _dailyTaskService = authenticator is AccountAuthenticator accountAuthenticator &&
                accountAuthenticator.AccountRepository is IDailyRewardRepository repository
                ? new DailyTaskService(repository) : null;
            _dailyTaskRequestSequences.Clear();
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
                    conn.Send(new AccountBalanceSync { Chips = account.Chips });
                }
                conn.Send(response);
            }, requireAuthentication: true);
        }

        private DailyTaskResult ValidateDailyTaskRequest(NetworkConnectionToClient conn, ulong requestId,
            out PlayerAccount account)
        {
            account = null;
            if (conn == null || !conn.isAuthenticated || !_playerManager.TryGetAccount(conn, out account))
                return DailyTaskResult.Unavailable;
            if (requestId == 0 || (_dailyTaskRequestSequences.TryGetValue(conn, out var last) && requestId <= last))
                return DailyTaskResult.InvalidRequest;
            _dailyTaskRequestSequences[conn] = requestId;
            RecordClientActivity(conn);
            if (_roomManager.WhichRoom(account.UserName, out _) || account.LastRoomId.HasValue)
                return DailyTaskResult.OutsideRoomRequired;
            return _dailyTaskService == null ? DailyTaskResult.Unavailable : DailyTaskResult.Success;
        }

        internal DailyTasksResponse ProcessDailyTasksRequest(NetworkConnectionToClient conn, DailyTasksRequest request)
        {
            var result = ValidateDailyTaskRequest(conn, request.RequestId, out var account);
            return new DailyTasksResponse
            {
                RequestId = request.RequestId,
                AccountId = account?.AccountId,
                Result = result,
                Snapshot = result == DailyTaskResult.Success ? _dailyTaskService.GetStatus(account.AccountId) : default,
            };
        }

        internal DailyTaskClaimResponse ProcessDailyTaskClaim(NetworkConnectionToClient conn,
            DailyTaskClaimRequest request, out PlayerAccount updatedAccount)
        {
            updatedAccount = null;
            var result = ValidateDailyTaskRequest(conn, request.RequestId, out var account);
            if (result == DailyTaskResult.Success)
                result = _dailyTaskService.Claim(account.AccountId, request.TaskId, request.Period, out updatedAccount);
            return new DailyTaskClaimResponse
            {
                RequestId = request.RequestId,
                AccountId = account?.AccountId,
                TaskId = request.TaskId,
                Result = result,
                GrantedAmount = result == DailyTaskResult.Success
                    ? (request.TaskId == DailyTaskId.DailySupply ? DailyTaskService.SupplyReward : DailyTaskService.ComebackReward)
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
            if (!CanOpenDailyTasks)
                return 0;
            var requestId = ++_nextDailyTaskRequest;
            NetworkClient.Send(new DailyTaskClaimRequest { RequestId = requestId, TaskId = taskId, Period = period });
            return requestId;
        }

        private void RegisterDailyTaskClientHandlers()
        {
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
    }
}
