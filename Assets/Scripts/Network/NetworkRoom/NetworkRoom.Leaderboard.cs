using System.Collections.Generic;
using Game.Network.Accounts;
using Game.UI;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private readonly Dictionary<(NetworkConnectionToClient Connection, LeaderboardKind Kind), (ulong Request, double NextQuery)> _leaderboardRequests =
            new Dictionary<(NetworkConnectionToClient Connection, LeaderboardKind Kind), (ulong Request, double NextQuery)>();
        private ulong _nextLeaderboardRequest;
        private readonly Dictionary<LeaderboardKind, ClientSnapshotCache<LeaderboardSnapshot>> _leaderboardCache =
            new Dictionary<LeaderboardKind, ClientSnapshotCache<LeaderboardSnapshot>>
            {
                [LeaderboardKind.Wealth] = new ClientSnapshotCache<LeaderboardSnapshot>(60d),
                [LeaderboardKind.Service] = new ClientSnapshotCache<LeaderboardSnapshot>(60d)
            };
        private readonly Dictionary<LeaderboardKind, (ulong Id, double Deadline)> _leaderboardPending =
            new Dictionary<LeaderboardKind, (ulong Id, double Deadline)>();

        public bool TryGetLeaderboard(LeaderboardKind kind, out LeaderboardSnapshot snapshot)
        {
            snapshot = default;
            return _leaderboardCache.TryGetValue(kind, out var cache) && cache.TryGet(Time.realtimeSinceStartupAsDouble, out snapshot);
        }

        private void ClearLeaderboardCache()
        {
            foreach (var cache in _leaderboardCache.Values) cache.Clear();
            _leaderboardPending.Clear();
        }

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
            var response = new LeaderboardResponse { RequestId = request.RequestId, Kind = request.Kind, Result = LeaderboardResult.Unavailable };
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated || !_playerManager.TryGetAccount(conn, out var account))
                return response;
            response.AccountId = account.AccountId;
            _leaderboardRequests.TryGetValue((conn, request.Kind), out var previous);
            if (request.RequestId == 0 || request.RequestId <= previous.Request ||
                !System.Enum.IsDefined(typeof(LeaderboardKind), request.Kind))
            {
                response.Result = LeaderboardResult.InvalidRequest;
                return response;
            }
            var now = Time.realtimeSinceStartupAsDouble;
            _leaderboardRequests[(conn, request.Kind)] = (request.RequestId, previous.NextQuery);
            if (now < previous.NextQuery)
            {
                response.Result = LeaderboardResult.RateLimited;
                return response;
            }
            if (!(authenticator is AccountAuthenticator accountAuthenticator) ||
                !(accountAuthenticator.AccountRepository is ILeaderboardRepository repository))
                return response;
            _leaderboardRequests[(conn, request.Kind)] = (request.RequestId, now + 2d);
            response.Snapshot = repository.ReadLeaderboard(account.AccountId, request.Kind);
            response.Result = LeaderboardResult.Success;
            return response;
        }

        public ulong RequestLeaderboard(LeaderboardKind kind = LeaderboardKind.All)
        {
            if (!CanOpenLeaderboard)
                return 0;
            if (_leaderboardPending.TryGetValue(kind, out var pending) && Time.realtimeSinceStartupAsDouble < pending.Deadline)
                return pending.Id;
            var requestId = ++_nextLeaderboardRequest;
            _leaderboardPending[kind] = (requestId, Time.realtimeSinceStartupAsDouble + 10d);
            NetworkClient.Send(new LeaderboardRequest { RequestId = requestId, Kind = kind });
            return requestId;
        }

        private void RegisterLeaderboardClientHandler()
        {
            NetworkClient.RegisterHandler<LeaderboardResponse>(response =>
            {
                if (!HasAuthenticatedClient || response.AccountId != GameController.Instance.State.AccountId ||
                    !_leaderboardPending.TryGetValue(response.Kind, out var pending) || response.RequestId != pending.Id) return;
                _leaderboardPending.Remove(response.Kind);
                if (response.Result == LeaderboardResult.Success && _leaderboardCache.TryGetValue(response.Kind, out var cache))
                    cache.Store(response.Snapshot, Time.realtimeSinceStartupAsDouble);
                if (CanOpenLeaderboard) LeaderboardPanel.Instance?.Receive(response);
            });
        }
    }
}
