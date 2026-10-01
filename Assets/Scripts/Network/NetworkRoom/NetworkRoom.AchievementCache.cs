using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using Game.Network.Achievements;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private sealed class AchievementPageLoad<T>
        {
            public readonly List<T> Rows = new List<T>();
            public ulong RequestId;
            public int Offset;
            public int Total = -1;
            public int Restarts;
            public long Revision;
            public long ChangeVersion;
            public string CatalogRevision;
            public double Deadline;
        }

        private readonly AchievementClientCache _clientAchievementCache = new AchievementClientCache();
        private AchievementPageLoad<AchievementDefinitionView> _achievementCatalogLoad;
        private AchievementPageLoad<AchievementStateView> _achievementStateLoad;
        private bool _achievementPanelOpen;
        private bool _achievementCatalogWanted;
        private bool _achievementStateWanted;
        private double _nextAchievementClientSend;
        private string _achievementCatalogError;
        private string _achievementStateError;
        private ulong _achievementClaimInFlight;
        private double _achievementClaimDeadline;
        public event Action AchievementCacheChanged;
        public event Action<AchievementClaimResponse> AchievementClaimReceived;
        public event Action<AchievementBatchClaimResponse> AchievementBatchClaimReceived;

        private bool IsCurrentAchievementSession => HasAuthenticatedClient &&
            _clientAchievementCache.AccountId == GameController.Instance.State.AccountId;
        public bool AchievementCacheBusy => _achievementCatalogLoad != null || _achievementStateLoad != null ||
            _achievementCatalogWanted || _achievementStateWanted;
        public bool AchievementProgressDirty => _clientAchievementCache.IsDirty;
        public string AchievementCacheError => _achievementStateError ?? _achievementCatalogError;
        public bool CanClaimCachedAchievements => IsCurrentAchievementSession && !_roomEntryPending;
        public ulong PendingAchievementClaim => _achievementClaimInFlight;

        private void BeginAchievementClientSession()
        {
            ClearAchievementClientSession();
            var web = transport as Mirror.SimpleWeb.SimpleWebTransport;
            var server = _clientConfiguration?.ServerUri.AbsoluteUri ??
                networkAddress + "|" + transport?.GetType().FullName + "|" + web?.clientUseWss + "|" + web?.clientWebsocketSettings.CustomClientPort;
            _clientAchievementCache.BeginSession(server, GameController.Instance.State.AccountId);
            // Login refreshes player state even if the achievement panel has never been opened.
            _achievementStateWanted = true;
        }

        private void ClearAchievementClientSession()
        {
            _clientAchievementCache.ClearSession();
            _achievementCatalogLoad = null;
            _achievementStateLoad = null;
            _achievementPanelOpen = _achievementCatalogWanted = _achievementStateWanted = false;
            _achievementClaimInFlight = 0;
            _achievementCatalogError = _achievementStateError = null;
            _nextAchievementClientSend = 0;
            // Request IDs remain monotonic across reconnects, rejecting responses from the old session.
            AchievementCacheChanged?.Invoke();
        }

        public void OpenCachedAchievements(bool forceStateRefresh = false)
        {
            _achievementPanelOpen = true;
            if (!IsCurrentAchievementSession) return;
            if (!_clientAchievementCache.HasCatalog && _achievementCatalogLoad == null) _achievementCatalogWanted = true;
            if (forceStateRefresh) _clientAchievementCache.MarkDirty();
            if (!_clientAchievementCache.HasStates || _clientAchievementCache.IsDirty)
            {
                if (_achievementStateLoad == null || _achievementStateLoad.ChangeVersion != _clientAchievementCache.ChangeVersion)
                    _achievementStateWanted = true;
            }
            AchievementCacheChanged?.Invoke();
        }

        public void CloseCachedAchievements() => _achievementPanelOpen = false;

        public bool TryGetCachedAchievements(out AchievementView[] views)
        {
            views = Array.Empty<AchievementView>();
            if (!IsCurrentAchievementSession || !_clientAchievementCache.HasCatalog || !_clientAchievementCache.HasStates) return false;
            views = _clientAchievementCache.BuildViews();
            return true;
        }

        private void TickAchievementClient()
        {
            if (!IsCurrentAchievementSession) return;
            var now = Time.realtimeSinceStartupAsDouble;
            if (_achievementClaimInFlight != 0 && now >= _achievementClaimDeadline)
            {
                _achievementClaimInFlight = 0;
                _clientAchievementCache.MarkDirty();
                _achievementStateWanted = true;
                _achievementStateError = "领取响应超时，正在确认状态…";
                AchievementCacheChanged?.Invoke();
            }
            if (_achievementStateWanted && _achievementStateLoad == null)
            {
                _achievementStateWanted = false;
                _achievementStateError = null;
                _achievementStateLoad = new AchievementPageLoad<AchievementStateView>();
            }
            if (_achievementCatalogWanted && _achievementCatalogLoad == null)
            {
                _achievementCatalogWanted = false;
                _achievementCatalogError = null;
                _achievementCatalogLoad = new AchievementPageLoad<AchievementDefinitionView>();
            }
            if (_achievementStateLoad?.RequestId > 0 && now >= _achievementStateLoad.Deadline)
                RestartAchievementStates("成就状态读取超时，请刷新重试。");
            if (_achievementCatalogLoad?.RequestId > 0 && now >= _achievementCatalogLoad.Deadline)
                RestartAchievementCatalog("成就目录读取超时，请刷新重试。");
            if (now < _nextAchievementClientSend) return;
            if (_achievementStateLoad != null && _achievementStateLoad.RequestId == 0)
            {
                var load = _achievementStateLoad;
                if (load.Offset == 0)
                {
                    load.ChangeVersion = _clientAchievementCache.ChangeVersion;
                    // This new first page covers every refresh requested before the send.
                    _achievementStateWanted = false;
                }
                load.RequestId = ++_nextAchievementRequest;
                load.Deadline = now + 10;
                _nextAchievementClientSend = now + 0.2;
                NetworkClient.Send(new AchievementStatesRequest { RequestId = load.RequestId, Offset = load.Offset,
                    Revision = load.Revision, CatalogRevision = load.CatalogRevision });
                AchievementCacheChanged?.Invoke();
            }
            else if (_achievementCatalogLoad != null && _achievementCatalogLoad.RequestId == 0)
            {
                var load = _achievementCatalogLoad;
                load.RequestId = ++_nextAchievementRequest;
                load.Deadline = now + 10;
                _nextAchievementClientSend = now + 0.2;
                NetworkClient.Send(new AchievementCatalogRequest { RequestId = load.RequestId, Offset = load.Offset, Revision = load.CatalogRevision });
                AchievementCacheChanged?.Invoke();
            }
        }

        private static bool AcceptAchievementPage<T>(AchievementPageLoad<T> load, int offset, int total, int nextOffset, T[] rows)
        {
            if (rows == null || offset != load.Offset || total < 0 || total < offset ||
                rows.Length > AchievementService.PageSize || rows.Length > total - offset ||
                load.Total >= 0 && load.Total != total) return false;
            var end = offset + rows.Length;
            if (nextOffset == -1 ? end != total : rows.Length == 0 || nextOffset != end || nextOffset >= total) return false;
            load.Total = total;
            load.Rows.AddRange(rows);
            load.Offset = nextOffset;
            return true;
        }

        private void ReceiveAchievementCatalog(AchievementCatalogResponse response)
        {
            var load = _achievementCatalogLoad;
            if (!IsCurrentAchievementSession || response.AccountId != _clientAchievementCache.AccountId ||
                load == null || load.RequestId == 0 || response.RequestId != load.RequestId) return;
            load.RequestId = 0;
            if (response.Result == AchievementResult.RevisionChanged)
            {
                RestartAchievementCatalog("成就目录发生变化，请刷新重试。");
                return;
            }
            if (response.Result != AchievementResult.Success || string.IsNullOrEmpty(response.Revision) ||
                load.Offset > 0 && load.CatalogRevision != response.Revision ||
                !AcceptAchievementPage(load, response.Offset, response.Total, response.NextOffset, response.Definitions))
            {
                FailAchievementCatalog("暂时无法读取成就目录，请刷新重试。");
                return;
            }
            load.CatalogRevision = response.Revision;
            if (load.Offset != -1) return;
            if (load.Rows.Any(d => !Game.Achievements.AchievementCatalog.IsValidId(d.Id)) ||
                load.Rows.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != load.Rows.Count)
            {
                FailAchievementCatalog("成就目录不完整，请刷新重试。");
                return;
            }
            if (!_clientAchievementCache.StoreCatalog(load.Rows.ToArray(), load.CatalogRevision))
            {
                RestartAchievementCatalog("成就目录发生变化，请刷新重试。");
                return;
            }
            _achievementCatalogLoad = null;
            _achievementCatalogError = null;
            AchievementCacheChanged?.Invoke();
        }

        private void ReceiveAchievementStates(AchievementStatesResponse response)
        {
            var load = _achievementStateLoad;
            if (!IsCurrentAchievementSession || response.AccountId != _clientAchievementCache.AccountId ||
                load == null || load.RequestId == 0 || response.RequestId != load.RequestId) return;
            load.RequestId = 0;
            if (response.Result == AchievementResult.RevisionChanged)
            {
                RestartAchievementStates("成就状态发生变化，请刷新重试。");
                return;
            }
            if (response.Result != AchievementResult.Success || string.IsNullOrEmpty(response.CatalogRevision) ||
                load.Offset > 0 && (load.Revision != response.Revision || load.CatalogRevision != response.CatalogRevision) ||
                !AcceptAchievementPage(load, response.Offset, response.Total, response.NextOffset, response.States))
            {
                FailAchievementStates("暂时无法读取成就状态，请刷新重试。");
                return;
            }
            load.Revision = response.Revision;
            load.CatalogRevision = response.CatalogRevision;
            if (load.Offset != -1) return;
            if (load.Rows.Any(s => !Game.Achievements.AchievementCatalog.IsValidId(s.Id)) ||
                load.Rows.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != load.Rows.Count)
            {
                FailAchievementStates("成就状态不完整，请刷新重试。");
                return;
            }
            if (!_clientAchievementCache.StoreStates(load.Rows.ToArray(), load.CatalogRevision, load.Revision, load.ChangeVersion))
            {
                RestartAchievementStates("成就状态发生变化，请刷新重试。");
                return;
            }
            _achievementStateLoad = null;
            _achievementStateError = null;
            if (_achievementPanelOpen && !_clientAchievementCache.HasCatalog && _achievementCatalogLoad == null)
                _achievementCatalogWanted = true;
            AchievementCacheChanged?.Invoke();
        }

        private static bool RestartAchievementLoad<T>(AchievementPageLoad<T> load)
        {
            if (load == null || ++load.Restarts > 2) return false;
            load.Rows.Clear();
            load.RequestId = 0;
            load.Offset = 0;
            load.Total = -1;
            load.Revision = 0;
            load.CatalogRevision = null;
            return true;
        }

        private void RestartAchievementStates(string error)
        {
            if (!RestartAchievementLoad(_achievementStateLoad)) FailAchievementStates(error);
        }

        private void RestartAchievementCatalog(string error)
        {
            if (!RestartAchievementLoad(_achievementCatalogLoad)) FailAchievementCatalog(error);
        }

        private void FailAchievementStates(string error)
        {
            _achievementStateLoad = null;
            _achievementStateWanted = false;
            _clientAchievementCache.MarkDirty();
            _achievementStateError = error;
            AchievementCacheChanged?.Invoke();
        }

        private void FailAchievementCatalog(string error)
        {
            _achievementCatalogLoad = null;
            _achievementCatalogWanted = false;
            _achievementCatalogError = error;
            AchievementCacheChanged?.Invoke();
        }
    }
}
