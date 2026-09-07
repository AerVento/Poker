using System.Collections.Generic;
using Game.Network.Accounts;
using Game.UI;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private readonly Dictionary<NetworkConnectionToClient, (ulong Request, double NextQuery)> _leaderboardRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Request, double NextQuery)>();
        private ulong _nextLeaderboardRequest;

        public bool CanOpenLeaderboard => HasAuthenticatedClient && !_roomEntryPending &&
            !_activeClientRoomId.HasValue && !GameController.Instance.State.LastRoomId.HasValue;

        private void RegisterLeaderboardServerHandler()
        {
            _leaderboardRequests.Clear();
            RegisterServerHandler<LeaderboardRequest>((conn, request) =>
                conn.Send(ProcessLeaderboardRequest(conn, request)), requireAuthentication: true);
        }

        internal LeaderboardResponse ProcessLeaderboardRequest(NetworkConnectionToClient conn, LeaderboardRequest request)
        {
            var response = new LeaderboardResponse { RequestId = request.RequestId, Result = LeaderboardResult.Unavailable };
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated || !_playerManager.TryGetAccount(conn, out var account))
                return response;
            response.AccountId = account.AccountId;
            _leaderboardRequests.TryGetValue(conn, out var previous);
            if (request.RequestId == 0 || request.RequestId <= previous.Request)
            {
                response.Result = LeaderboardResult.InvalidRequest;
                return response;
            }
            var now = Time.realtimeSinceStartupAsDouble;
            _leaderboardRequests[conn] = (request.RequestId, previous.NextQuery);
            if (now < previous.NextQuery)
            {
                response.Result = LeaderboardResult.RateLimited;
                return response;
            }
            if (!(authenticator is AccountAuthenticator accountAuthenticator) ||
                !(accountAuthenticator.AccountRepository is ILeaderboardRepository repository))
                return response;
            _leaderboardRequests[conn] = (request.RequestId, now + 2d);
            response.Snapshot = repository.ReadLeaderboard(account.AccountId);
            response.Result = LeaderboardResult.Success;
            return response;
        }

        public ulong RequestLeaderboard()
        {
            if (!CanOpenLeaderboard)
                return 0;
            var requestId = ++_nextLeaderboardRequest;
            NetworkClient.Send(new LeaderboardRequest { RequestId = requestId });
            return requestId;
        }

        private void RegisterLeaderboardClientHandler()
        {
            NetworkClient.RegisterHandler<LeaderboardResponse>(response =>
            {
                if (CanOpenLeaderboard && response.AccountId == GameController.Instance.State.AccountId)
                    LeaderboardPanel.Instance?.Receive(response);
            });
        }
    }
}
