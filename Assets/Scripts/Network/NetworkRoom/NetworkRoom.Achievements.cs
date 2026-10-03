using System;
using System.Collections.Generic;
using Game.Achievements;
using Game.Network.Accounts;
using Game.Network.Achievements;
using Game.UI;
using Framework.UI;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private AchievementService _achievements;
        private double _nextAchievementTick;
        private double _nextAchievementAnnouncements;
        private ulong _nextAchievementRequest;
        private readonly HashSet<string> _achievementProgressPending = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<NetworkConnectionToClient> _achievementDirtySent = new HashSet<NetworkConnectionToClient>();
        private readonly Dictionary<NetworkConnectionToClient, HashSet<string>> _achievementSent =
            new Dictionary<NetworkConnectionToClient, HashSet<string>>();
        private readonly Dictionary<NetworkConnectionToClient, (ulong Sequence, double Window, int Count)> _achievementRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Sequence, double Window, int Count)>();

        public bool CanViewAchievements => HasAuthenticatedClient && !_roomEntryPending;

        private void RegisterAchievementServerHandlers()
        {
            if (_serverStorageFailed) return;
            try
            {
                var repository = (authenticator as AccountAuthenticator)?.AccountRepository as IAchievementRepository;
                if (repository == null) throw new InvalidOperationException("Achievements require account storage.");
                ContractAchievementFacts.Catalog = ItemCatalog;
                _achievements = new AchievementService(repository, AchievementCatalog.Discover(), id =>
                    ItemCatalog?.MaxQuantityOf(id));
                _achievements.Initialize();
                _achievements.ProgressChanged += OnAchievementProgressChanged;
            }
            catch (Exception exception)
            {
                HandleFatalStorageError(exception);
                return;
            }
            RegisterServerHandler<AchievementCatalogRequest>((conn, request) =>
            {
                var result = ValidateAchievementRequest(conn, request.RequestId, out var account);
                var response = result == AchievementResult.Success
                    ? _achievements.GetDefinitions(account.AccountId, request.Offset, request.Revision)
                    : new AchievementCatalogResponse { AccountId = account?.AccountId, Result = result };
                response.RequestId = request.RequestId;
                conn.Send(response);
            }, requireAuthentication: true);
            RegisterServerHandler<AchievementStatesRequest>((conn, request) =>
            {
                var result = ValidateAchievementRequest(conn, request.RequestId, out var account);
                var response = result == AchievementResult.Success
                    ? _achievements.GetStates(account.AccountId, request.Offset, request.Revision, request.CatalogRevision)
                    : new AchievementStatesResponse { AccountId = account?.AccountId, Result = result };
                if (response.Result == AchievementResult.Success && request.Offset == 0) _achievementDirtySent.Remove(conn);
                response.RequestId = request.RequestId;
                conn.Send(response);
            }, requireAuthentication: true);
            RegisterServerHandler<AchievementClaimRequest>((conn, request) =>
            {
                var response = ProcessAchievementClaim(conn, request, out var account);
                if (account != null)
                {
                    if (_playerManager.TryGetSession(conn, out var session)) session.Account = account.Copy();
                    SynchronizeWalletBalance(account);
                    conn.Send(new AccountBalanceSync { Chips = account.Chips, Dollars = account.Dollars });
                }
                conn.Send(response);
                if (account != null) conn.Send(ReadCollection(conn));
            }, requireAuthentication: true);
            RegisterServerHandler<AchievementBatchClaimRequest>((conn, request) =>
            {
                var response = ProcessAchievementBatchClaim(conn, request, out var account);
                if (account != null)
                {
                    if (_playerManager.TryGetSession(conn, out var session)) session.Account = account.Copy();
                    SynchronizeWalletBalance(account);
                    conn.Send(new AccountBalanceSync { Chips = account.Chips, Dollars = account.Dollars });
                }
                conn.Send(response);
                if (account != null) conn.Send(ReadCollection(conn));
            }, requireAuthentication: true);
            RegisterServerHandler<AchievementAnnouncementAck>((conn, request) =>
            {
                if (_serverStorageFailed || _achievements == null || !conn.isAuthenticated ||
                    !_playerManager.TryGetAccount(conn, out var account) || !AchievementCatalog.IsValidId(request.AchievementId) ||
                    !_achievementSent.TryGetValue(conn, out var sent) || !sent.Contains(request.AchievementId)) return;
                _achievements.Acknowledge(account.AccountId, request.AchievementId);
                sent.Remove(request.AchievementId);
            }, requireAuthentication: true);
        }

        private void OnAchievementProgressChanged(string accountId) => _achievementProgressPending.Add(accountId);

        private void TickAchievements()
        {
            TickAchievementClient();
            if (!NetworkServer.active || _achievements == null || _serverStorageFailed ||
                Time.realtimeSinceStartupAsDouble < _nextAchievementTick) return;
            _nextAchievementTick = Time.realtimeSinceStartupAsDouble + 0.1;
            try
            {
                _achievements.DrainEvents();
                if (Time.realtimeSinceStartupAsDouble < _nextAchievementAnnouncements) return;
                _nextAchievementAnnouncements = Time.realtimeSinceStartupAsDouble + 1;
                foreach (var conn in NetworkServer.connections.Values)
                {
                    if (conn == null || !conn.isAuthenticated || !_playerManager.TryGetAccount(conn, out var account)) continue;
                    if (!_achievementSent.TryGetValue(conn, out var sent))
                        _achievementSent.Add(conn, sent = new HashSet<string>(StringComparer.Ordinal));
                    var unlocked = false;
                    foreach (var achievement in _achievements.PendingAnnouncements(account.AccountId))
                    {
                        if (!sent.Add(achievement.Id)) continue;
                        conn.Send(new AchievementUnlocked { AccountId = account.AccountId, Achievement = achievement });
                        unlocked = true;
                    }
                    // Coalesce progress-only changes until this client starts fetching state again.
                    if (!unlocked && _achievementProgressPending.Contains(account.AccountId) && _achievementDirtySent.Add(conn))
                        conn.Send(new AchievementProgressChanged { Changed = true });
                }
                _achievementProgressPending.Clear();
            }
            catch (Exception exception) { HandleFatalStorageError(exception); }
        }

        private AchievementResult ValidateAchievementRequest(NetworkConnectionToClient conn, ulong requestId, out PlayerAccount account)
        {
            account = null;
            if (_serverStorageFailed || _achievements == null || conn == null || !conn.isAuthenticated ||
                !_playerManager.TryGetAccount(conn, out account)) return AchievementResult.Unavailable;
            _achievementRequests.TryGetValue(conn, out var prior);
            if (requestId == 0 || requestId <= prior.Sequence) return AchievementResult.InvalidRequest;
            var now = Time.realtimeSinceStartupAsDouble;
            if (now - prior.Window >= 1) prior = (prior.Sequence, now, 0);
            _achievementRequests[conn] = (requestId, prior.Window, prior.Count + 1);
            return prior.Count >= 12 ? AchievementResult.RateLimited : AchievementResult.Success;
        }

        internal AchievementClaimResponse ProcessAchievementClaim(NetworkConnectionToClient conn, AchievementClaimRequest request, out PlayerAccount updated)
        {
            updated = null;
            var result = ValidateAchievementRequest(conn, request.RequestId, out var account);
            var response = result == AchievementResult.Success
                ? _achievements.Claim(account.AccountId, request.AchievementId, out updated)
                : new AchievementClaimResponse { AccountId = account?.AccountId, AchievementId = request.AchievementId, Result = result };
            response.RequestId = request.RequestId;
            return response;
        }

        internal AchievementBatchClaimResponse ProcessAchievementBatchClaim(NetworkConnectionToClient conn,
            AchievementBatchClaimRequest request, out PlayerAccount updated)
        {
            updated = null;
            var result = ValidateAchievementRequest(conn, request.RequestId, out var account);
            var response = result == AchievementResult.Success
                ? _achievements.ClaimAll(account.AccountId, request.Category, out updated)
                : new AchievementBatchClaimResponse
                {
                    AccountId = account?.AccountId,
                    Category = request.Category,
                    Result = result,
                    States = Array.Empty<AchievementStateView>(),
                    GrantedReward = Reward.Empty
                };
            response.RequestId = request.RequestId;
            return response;
        }

        public ulong RequestAchievementClaim(string id)
        {
            if (!CanClaimCachedAchievements || _achievementClaimInFlight != 0) return 0;
            var request = new AchievementClaimRequest { RequestId = ++_nextAchievementRequest, AchievementId = id };
            _achievementClaimInFlight = request.RequestId;
            _achievementClaimDeadline = Time.realtimeSinceStartupAsDouble + 10;
            NetworkClient.Send(request);
            return request.RequestId;
        }

        public ulong RequestAchievementBatchClaim(string category)
        {
            if (!CanClaimCachedAchievements || _achievementClaimInFlight != 0) return 0;
            var request = new AchievementBatchClaimRequest
                { RequestId = ++_nextAchievementRequest, Category = category };
            _achievementClaimInFlight = request.RequestId;
            _achievementClaimDeadline = Time.realtimeSinceStartupAsDouble + 10;
            NetworkClient.Send(request);
            return request.RequestId;
        }

        private void RegisterAchievementClientHandlers()
        {
            ClearAchievementClient();
            NetworkClient.RegisterHandler<AchievementCatalogResponse>(ReceiveAchievementCatalog);
            NetworkClient.RegisterHandler<AchievementStatesResponse>(ReceiveAchievementStates);
            NetworkClient.RegisterHandler<AchievementProgressChanged>(message =>
            {
                if (!IsCurrentAchievementSession || !message.Changed) return;
                _clientAchievementCache.MarkDirty();
                // Ordinary progress is lazy: do not download states until the next open/manual refresh.
                AchievementCacheChanged?.Invoke();
            });
            NetworkClient.RegisterHandler<AchievementClaimResponse>(response =>
            {
                if (!IsCurrentAchievementSession || response.AccountId != _clientAchievementCache.AccountId ||
                    response.RequestId != _achievementClaimInFlight) return;
                _achievementClaimInFlight = 0;
                if (response.HasState)
                {
                    _clientAchievementCache.ApplyClaim(response.State, response.Revision);
                    if (_achievementStateLoad != null) _achievementStateWanted = true;
                }
                AchievementClaimReceived?.Invoke(response);
                AchievementCacheChanged?.Invoke();
            });
            NetworkClient.RegisterHandler<AchievementBatchClaimResponse>(response =>
            {
                if (!IsCurrentAchievementSession || response.AccountId != _clientAchievementCache.AccountId ||
                    response.RequestId != _achievementClaimInFlight) return;
                _achievementClaimInFlight = 0;
                if (response.Result == AchievementResult.Success)
                {
                    _clientAchievementCache.ApplyClaims(response.States, response.Revision);
                    if (_achievementStateLoad != null) _achievementStateWanted = true;
                }
                AchievementBatchClaimReceived?.Invoke(response);
                AchievementCacheChanged?.Invoke();
            });
            NetworkClient.RegisterHandler<AchievementUnlocked>(message =>
            {
                if (!IsCurrentAchievementSession || message.AccountId != _clientAchievementCache.AccountId) return;
                _clientAchievementCache.ApplyUnlocked(message.Achievement);
                _achievementStateWanted = true;
                AchievementCacheChanged?.Invoke();
                if (!UIManager.Available) return;
                var banner = UIManager.Instance.PushPanel<AchievementBannerPanel>(UIManager.Layer.System);
                banner.Enqueue(message.Achievement, () =>
                {
                    if (HasAuthenticatedClient && message.AccountId == GameController.Instance.State.AccountId)
                        NetworkClient.Send(new AchievementAnnouncementAck { AchievementId = message.Achievement.Id });
                });
            });
        }

        private void ClearAchievementClient()
        {
            // The process-local catalog contains only masked hidden definitions. Real hidden metadata lives in
            // account state and is discarded here with every other account-specific value.
            ClearAchievementClientSession();
            if (UIManager.Available) UIManager.Instance.DestroyPanel(nameof(AchievementBannerPanel));
        }

        private void ClearAchievementServer()
        {
            if (_achievements != null) _achievements.ProgressChanged -= OnAchievementProgressChanged;
            _achievements = null;
            _achievementProgressPending.Clear();
            _achievementDirtySent.Clear();
            _achievementRequests.Clear();
            _achievementSent.Clear();
            _nextAchievementTick = 0;
            _nextAchievementAnnouncements = 0;
        }
    }
}
