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
                _achievements = new AchievementService(repository, AchievementCatalog.Discover(), id =>
                    NamecardItemId.TryToNamecardId(id, out _)
                        ? ItemIdentities != null && ItemIdentities.TryGet(id, out var item) ? (uint?)item.MaxQuantity : null
                        : _itemRuntime?.Find(id)?.MaxQuantity);
                _achievements.Initialize();
            }
            catch (Exception exception)
            {
                HandleFatalStorageError(exception);
                return;
            }
            RegisterServerHandler<AchievementsRequest>((conn, request) => conn.Send(ProcessAchievementsRequest(conn, request)), requireAuthentication: true);
            RegisterServerHandler<AchievementClaimRequest>((conn, request) =>
            {
                var response = ProcessAchievementClaim(conn, request, out var account);
                if (account != null)
                {
                    if (_playerManager.TryGetSession(conn, out var session)) session.Account = account.Copy();
                    conn.Send(new AccountBalanceSync { Chips = account.Chips });
                }
                conn.Send(response);
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

        private void TickAchievements()
        {
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
                    foreach (var achievement in _achievements.PendingAnnouncements(account.AccountId))
                        if (sent.Add(achievement.Id)) conn.Send(new AchievementUnlocked { AccountId = account.AccountId, Achievement = achievement });
                }
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

        internal AchievementsResponse ProcessAchievementsRequest(NetworkConnectionToClient conn, AchievementsRequest request)
        {
            var result = ValidateAchievementRequest(conn, request.RequestId, out var account);
            var response = result == AchievementResult.Success
                ? _achievements.GetStatus(account.AccountId, request.Offset, request.Revision)
                : new AchievementsResponse { AccountId = account?.AccountId, Result = result };
            response.RequestId = request.RequestId;
            return response;
        }

        internal AchievementClaimResponse ProcessAchievementClaim(NetworkConnectionToClient conn, AchievementClaimRequest request, out PlayerAccount updated)
        {
            updated = null;
            var result = ValidateAchievementRequest(conn, request.RequestId, out var account);
            if (result == AchievementResult.Success && (_roomManager.WhichRoom(account.UserName, out _) || account.LastRoomId.HasValue))
                result = AchievementResult.OutsideRoomRequired;
            var response = result == AchievementResult.Success
                ? _achievements.Claim(account.AccountId, request.AchievementId, out updated)
                : new AchievementClaimResponse { AccountId = account?.AccountId, AchievementId = request.AchievementId, Result = result };
            response.RequestId = request.RequestId;
            return response;
        }

        public ulong RequestAchievements(int offset = 0, long revision = 0)
        {
            if (!CanViewAchievements) return 0;
            var request = new AchievementsRequest { RequestId = ++_nextAchievementRequest, Offset = offset, Revision = revision };
            NetworkClient.Send(request);
            return request.RequestId;
        }

        public ulong RequestAchievementClaim(string id)
        {
            if (!CanViewAchievements) return 0;
            var request = new AchievementClaimRequest { RequestId = ++_nextAchievementRequest, AchievementId = id };
            NetworkClient.Send(request);
            return request.RequestId;
        }

        private void RegisterAchievementClientHandlers()
        {
            ClearAchievementClient();
            NetworkClient.RegisterHandler<AchievementsResponse>(response =>
            {
                if (HasAuthenticatedClient && response.AccountId == GameController.Instance.State.AccountId)
                    AchievementPanel.Instance?.ReceiveStatus(response);
            });
            NetworkClient.RegisterHandler<AchievementClaimResponse>(response =>
            {
                if (HasAuthenticatedClient && response.AccountId == GameController.Instance.State.AccountId)
                    AchievementPanel.Instance?.ReceiveClaim(response);
            });
            NetworkClient.RegisterHandler<AchievementUnlocked>(message =>
            {
                if (!HasAuthenticatedClient || message.AccountId != GameController.Instance.State.AccountId || !UIManager.Available) return;
                var banner = UIManager.Instance.PushPanel<AchievementBannerPanel>(UIManager.Layer.System);
                banner.Enqueue(message.Achievement, () =>
                {
                    if (HasAuthenticatedClient && message.AccountId == GameController.Instance.State.AccountId)
                        NetworkClient.Send(new AchievementAnnouncementAck { AchievementId = message.Achievement.Id });
                });
                AchievementPanel.Instance?.AchievementChanged();
            });
        }

        private void ClearAchievementClient()
        {
            _nextAchievementRequest = 0;
            if (UIManager.Available) UIManager.Instance.DestroyPanel(nameof(AchievementBannerPanel));
        }

        private void ClearAchievementServer()
        {
            _achievements = null;
            _achievementRequests.Clear();
            _achievementSent.Clear();
            _nextAchievementTick = 0;
            _nextAchievementAnnouncements = 0;
        }
    }
}
